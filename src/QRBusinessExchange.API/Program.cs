using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using QRBusinessExchange.API.Middleware;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Data;
using QRBusinessExchange.Infrastructure.Repositories;
using QRBusinessExchange.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure database
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        "Server=incacorporation.net;Database=www.qrbound.com;User Id=ssoAdministrator;Password=8^7lhy1F;TrustServerCertificate=True;Encrypt=False;",
        b => b.MigrationsAssembly("QRBusinessExchange.Infrastructure")
    ));

// Configure settings
builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection("JwtSettings"));
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<PaymentSettings>(builder.Configuration.GetSection("PaymentSettings"));
builder.Services.Configure<OneSignalSettings>(builder.Configuration.GetSection("OneSignalSettings"));

// Configure JWT authentication
var jwtSettings = builder.Configuration.GetSection("JwtSettings").Get<JwtSettings>() 
    ?? new JwtSettings { SecretKey = "YourSuperSecretKeyForJWTTokenGenerationMinimum256Bits!", Issuer = "QRBusinessExchange", Audience = "QRBusinessExchangeAPI" };

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
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SecretKey)),
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

// Register services
builder.Services.AddHttpClient(); // Required for OneSignalService
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<IQRCodeService, QRCodeService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IGeolocationService, GeolocationService>();
builder.Services.AddScoped<INotificationService, OneSignalService>();

// Configure controllers
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });

// Configure Swagger/OpenAPI
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

    // Add JWT authentication to Swagger
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

// Initialize database and seed data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        await DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

// Configure middleware pipeline
app.UseMiddleware<ErrorHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "QR Business Exchange API v1");
    });
}

app.UseHttpsRedirection();

app.UseCors("AllowBlazorWeb");

app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<RateLimitingMiddleware>();
app.UseMiddleware<JwtMiddleware>();

app.MapControllers();

app.Run();

// Make Program class accessible to integration tests
public partial class Program { }

