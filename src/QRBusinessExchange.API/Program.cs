using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Scalar.AspNetCore;
using QRBusinessExchange.API.Middleware;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Data;
using QRBusinessExchange.Infrastructure.Repositories;
using QRBusinessExchange.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure database
// The connection string (no password) is in appsettings.json; the password comes from
// user-secrets in development or the Database__Password environment variable on servers.
var databaseConnection = DatabaseConnection.Build(
    builder.Configuration.GetConnectionString(DatabaseConnection.ConnectionStringKey),
    builder.Configuration[DatabaseConnection.PasswordKey],
    "QRBusinessExchange.API");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        databaseConnection.ConnectionString,
        b => b.MigrationsAssembly("QRBusinessExchange.Infrastructure")
    ));

// Configure settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<PaymentSettings>(builder.Configuration.GetSection("PaymentSettings"));
builder.Services.Configure<OneSignalSettings>(builder.Configuration.GetSection("OneSignalSettings"));

// Configure JWT authentication. The signing key comes only from user-secrets (development) or the
// JwtSettings__SecretKey environment variable (servers); startup stops if it's missing, short or a sample value.
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() ?? new JwtSettings();
var jwtSigningKey = JwtSettings.GetValidatedKeyBytes(jwtSettings.SecretKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettings.Issuer,
        ValidAudience = jwtSettings.Audience,
        IssuerSigningKey = new SymmetricSecurityKey(jwtSigningKey),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.Zero
    };
});

builder.Services.AddAuthorization();

// Configure CORS - Allow Web app with credentials
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowBlazorWeb", policy =>
    {
        policy.WithOrigins(
                "https://localhost:7089",  // Blazor Web HTTPS
                "http://localhost:5116"    // Blazor Web HTTP (for development)
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()  // CRITICAL: Allow cookies/credentials
            .SetIsOriginAllowed(origin => true); // Allow dynamic origins in dev
    });
});

// Register repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IConnectionRepository, ConnectionRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();
builder.Services.AddScoped<IPasskeyRepository, PasskeyRepository>();

// Register services
builder.Services.AddHttpClient(); // Required for OneSignalService
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<IQRCodeService, QRCodeService>();
builder.Services.AddScoped<IEmailService>(provider =>
{
    var emailSettings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EmailSettings>>();
    var context = provider.GetRequiredService<ApplicationDbContext>();
    var logger = provider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<EmailService>>();
    var portalBaseUrl = builder.Configuration["Sso:PortalBaseUrl"] ?? "https://qrbusinessexchange.com";
    return new EmailService(emailSettings, context, logger, portalBaseUrl);
});
builder.Services.AddScoped<IGeolocationService, GeolocationService>();
builder.Services.AddScoped<INotificationService, OneSignalService>();
builder.Services.AddScoped<WebAuthnService>();

// CardPointe card payments. ApiUsername/ApiPassword come from user-secrets or
// CardPointe__ApiUsername / CardPointe__ApiPassword environment variables, never appsettings.
builder.Services.Configure<QRBusinessExchange.API.Services.CardPointeOptions>(
    builder.Configuration.GetSection(QRBusinessExchange.API.Services.CardPointeOptions.SectionName));
builder.Services.AddHttpClient<QRBusinessExchange.API.Services.ICardPointeService, QRBusinessExchange.API.Services.CardPointeService>(
    (sp, http) =>
    {
        var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<QRBusinessExchange.API.Services.CardPointeOptions>>().Value;
        http.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
    });
// QR payments: staff show a QR code, anyone who scans it pays by card.
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<QRBusinessExchange.API.Services.QrPaymentService>();

// Password-reset links: signed and encrypted with Data Protection keys kept on the server.
// In containers, persist the keys (PersistKeysToFileSystem on a volume) so links survive restarts.
builder.Services.AddDataProtection().SetApplicationName("QRBusinessExchange");
builder.Services.AddSingleton<QRBusinessExchange.API.Services.PasswordResetTokens>();
// Email-verification links: same Data Protection keys, separate purpose, 24-hour lifetime.
builder.Services.AddSingleton<QRBusinessExchange.API.Services.EmailVerificationTokens>();

// Multi-business QR / SSO login core
builder.Services.AddMemoryCache(); // WebAuthn challenges (use a distributed cache when scaling out)
builder.Services.Configure<SsoSettings>(builder.Configuration.GetSection("Sso"));
builder.Services.AddSingleton(sp =>
{
    var sso = builder.Configuration.GetSection("Sso").Get<SsoSettings>() ?? new SsoSettings();
    var keyPath = Path.IsPathRooted(sso.SigningKeyPath)
        ? sso.SigningKeyPath
        : Path.Combine(builder.Environment.ContentRootPath, sso.SigningKeyPath);
    return new SsoSigningKeyProvider(keyPath);
});
builder.Services.AddScoped<BusinessService>();
builder.Services.AddScoped<SsoService>();
builder.Services.AddScoped<SsoAccessService>();
builder.Services.AddScoped<QrLoginService>();
builder.Services.AddScoped<ContactService>();
builder.Services.AddScoped<NearbyService>();
builder.Services.AddScoped<ProfileImageService>();
builder.Services.AddSingleton<DeviceLinkService>(); // in-memory, short-lived "link a phone" sessions
builder.Services.AddScoped<QRBusinessExchange.API.Services.PortalSessionFactory>();

// Configure controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Configure OpenAPI documentation (used by Scalar)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "QR Business Exchange API",
        Version = "v1",
        Description = "API for QR Business Exchange platform - Connect, Exchange, and Network",
        Contact = new OpenApiContact
        {
            Name = "QR Business Exchange",
            Email = "support@qrbusinessexchange.com"
        }
    });

    // Use fully qualified names to avoid schema conflicts
    options.CustomSchemaIds(type => type.FullName?.Replace("+", "."));

    // Add JWT authentication to OpenAPI
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter your JWT token in the format: Bearer {your token}"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML comments if available
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

if (databaseConnection.TrustServerCertificate)
{
    app.Logger.LogWarning(
        "Database traffic to {Server} is encrypted, but the server's certificate is not verified " +
        "(TrustServerCertificate=True). Install a trusted certificate on SQL Server and set TrustServerCertificate=False.",
        databaseConnection.DataSource);
}

// Initialize database and seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await DbInitializer.Initialize(context, services.GetRequiredService<ILogger<Program>>(), app.Configuration["Admin:SeedPassword"]);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure middleware pipeline
app.UseMiddleware<ErrorHandlingMiddleware>();

app.UseHttpsRedirection();

app.UseCors("AllowBlazorWeb");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<JwtMiddleware>();

// Map all API controllers first (required for OpenAPI documentation to discover endpoints)
app.MapControllers();

// Enable OpenAPI middleware (endpoint serving the OpenAPI spec JSON for Scalar)
// Must be after MapControllers so endpoints are discovered
app.UseSwagger();

// Add Scalar for interactive API documentation (accessible at /scalar/v1)
// Scalar reads the OpenAPI spec that Swashbuckle serves at /swagger/v1/swagger.json (its own default,
// /openapi/v1.json, doesn't exist here, which left the page with no endpoints).
app.MapScalarApiReference(options => options.WithOpenApiRoutePattern("/swagger/{documentName}/swagger.json"));

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }

