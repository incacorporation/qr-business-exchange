using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qrbound.POS;
using Qrbound.POS.Data;
using Qrbound.POS.Services;
using Qrbound.SignIn.Server;

namespace Microsoft.Extensions.DependencyInjection;

public static class QrboundPosServiceCollectionExtensions
{
    /// <summary>
    /// Adds the restaurant POS: its SQL Server database (from configuration), the menu, ordering and
    /// checkout services, the qrbound payment clients, staff sign-in and the background payment check.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The app's configuration; the <c>QrboundPos</c> section and <c>ConnectionStrings</c> are read.</param>
    /// <param name="environment">Used to refuse simulated payments outside Development.</param>
    public static IServiceCollection AddQrboundPos(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection(QrboundPosOptions.SectionName);
        var settings = section.Get<QrboundPosOptions>() ?? new QrboundPosOptions();
        services.Configure<QrboundPosOptions>(section);

        if (settings.Payments.Simulate && !environment.IsDevelopment())
        {
            throw new InvalidOperationException("QrboundPos:Payments:Simulate only works in the Development environment.");
        }

        var connectionString = !string.IsNullOrWhiteSpace(settings.ConnectionString)
            ? settings.ConnectionString
            : configuration.GetConnectionString(settings.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"No SQL Server database for the POS: set ConnectionStrings:{settings.ConnectionStringName}, " +
                "point QrboundPos:ConnectionStringName at another connection string, or set QrboundPos:ConnectionString.");
        }

        services.AddDbContextFactory<PosDbContext>(db => db.UseSqlServer(connectionString, sql =>
        {
            sql.MigrationsHistoryTable("__EFMigrationsHistory", PosDbContext.Schema);
            sql.EnableRetryOnFailure(3);
            if (settings.CommandTimeoutSeconds is { } timeout) sql.CommandTimeout(timeout);
        }));

        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<PosEvents>();
        services.AddScoped<RestaurantService>();
        services.AddScoped<MenuService>();
        services.AddScoped<OrderService>();
        services.AddScoped<CheckoutService>();
        services.AddScoped<StaffService>();
        services.AddScoped<PosLinks>();
        services.AddHostedService<PaymentChecker>();

        // Qrbound.Payments: the customer's card form (QrboundPayLink) runs in the server circuit, so
        // its calls to the qrbound API go from this server; no CORS needed.
        var apiBase = new Uri(settings.Qrbound.ApiBaseUrl.TrimEnd('/') + "/");
        services.AddQrboundPayments(apiBase);
        services.AddHttpClient<QrboundBusinessClient>(http =>
        {
            http.BaseAddress = apiBase;
            http.Timeout = TimeSpan.FromSeconds(30);
        });

        // Customers sign in with qrbound; the server exchanges the code with the client secret and
        // checks the ID token's signature against qrbound's published keys.
        var customers = settings.CustomerSignIn;
        if (customers.Simulate && !environment.IsDevelopment())
        {
            throw new InvalidOperationException("QrboundPos:CustomerSignIn:Simulate only works in the Development environment.");
        }

        if (customers.Required && !customers.Simulate)
        {
            var redirectUri = customers.RedirectUri
                ?? (string.IsNullOrWhiteSpace(settings.PublicBaseUrl) ? null : settings.PublicBaseUrl.TrimEnd('/') + CustomerSignIn.CallbackPath);
            if (string.IsNullOrWhiteSpace(customers.ClientId) || redirectUri is null)
            {
                throw new InvalidOperationException(
                    "Customers sign in with qrbound, so the POS needs a qrbound SSO application: set QrboundPos:CustomerSignIn:ClientId, " +
                    "ClientSecret (user secrets) and RedirectUri (or QrboundPos:PublicBaseUrl). For local tries only, set " +
                    "QrboundPos:CustomerSignIn:Simulate in Development, or turn QrboundPos:CustomerSignIn:Required off.");
            }

            var authority = (customers.Authority ?? settings.Qrbound.ApiBaseUrl).TrimEnd('/');
            services.AddQrboundServer(o =>
            {
                o.Authority = authority;
                o.ClientId = customers.ClientId!;
                o.ClientSecret = customers.ClientSecret;
                o.RedirectUri = redirectUri;
                o.RequireHttpsMetadata = authority.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
            });
        }

        services.AddScoped<CurrentCustomer>();

        // Staff sign in with a cookie of their own scheme, customers with another. The POS's combined
        // scheme reads both and is the default only when the host app hasn't chosen one.
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, PosCombinedAuthenticationHandler>(PosCombinedAuthenticationHandler.SchemeName, null)
            .AddCookie(QrboundPosDefaults.CustomerAuthenticationScheme, cookie =>
            {
                cookie.Cookie.Name = ".QrboundPos.Customer";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                cookie.LoginPath = CustomerSignIn.SignInPath;
                cookie.LogoutPath = CustomerSignIn.SignOutPath;
                cookie.AccessDeniedPath = CustomerSignIn.ErrorPath;
                cookie.ExpireTimeSpan = TimeSpan.FromHours(12);
                cookie.SlidingExpiration = true;
            })
            .AddCookie(QrboundPosDefaults.AuthenticationScheme, cookie =>
            {
                cookie.Cookie.Name = ".QrboundPos.Staff";
                cookie.Cookie.HttpOnly = true;
                cookie.Cookie.SameSite = SameSiteMode.Lax;
                cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                cookie.LoginPath = "/pos/signin";
                cookie.LogoutPath = "/pos/signout";
                cookie.AccessDeniedPath = "/pos/denied";
                cookie.ExpireTimeSpan = TimeSpan.FromHours(12);
                cookie.SlidingExpiration = true;
                cookie.Events.OnValidatePrincipal = ValidateStaffAsync;
            });
        services.PostConfigure<AuthenticationOptions>(o =>
        {
            o.DefaultScheme ??= PosCombinedAuthenticationHandler.SchemeName;
        });

        services.AddAuthorizationBuilder()
            .AddPolicy(QrboundPosDefaults.StaffPolicy, p => p
                .AddAuthenticationSchemes(QrboundPosDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole(nameof(StaffRole.Staff), nameof(StaffRole.Manager), nameof(StaffRole.Owner)))
            .AddPolicy(QrboundPosDefaults.ManagerPolicy, p => p
                .AddAuthenticationSchemes(QrboundPosDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole(nameof(StaffRole.Manager), nameof(StaffRole.Owner)))
            .AddPolicy(QrboundPosDefaults.OwnerPolicy, p => p
                .AddAuthenticationSchemes(QrboundPosDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .RequireRole(nameof(StaffRole.Owner)))
            .AddPolicy(QrboundPosDefaults.CustomerPolicy, p =>
            {
                if (customers.Required)
                {
                    p.AddAuthenticationSchemes(QrboundPosDefaults.CustomerAuthenticationScheme)
                        .RequireAuthenticatedUser()
                        .RequireClaim(CustomerSignIn.SubjectClaim);
                }
                else
                {
                    p.RequireAssertion(_ => true);
                }
            });

        services.AddCascadingAuthenticationState();
        return services;
    }

    /// <summary>Signs staff out when their account is switched off, and picks up role changes.</summary>
    private static async Task ValidateStaffAsync(CookieValidatePrincipalContext context)
    {
        var id = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(id, out var staffId))
        {
            context.RejectPrincipal();
            return;
        }

        var staff = context.HttpContext.RequestServices.GetRequiredService<StaffService>();
        var role = await staff.CurrentRoleAsync(staffId);
        if (role is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(QrboundPosDefaults.AuthenticationScheme);
            return;
        }

        if (!context.Principal!.IsInRole(role.Value.ToString()))
        {
            var identity = (ClaimsIdentity)context.Principal.Identity!;
            foreach (var c in identity.FindAll(ClaimTypes.Role).ToList()) identity.RemoveClaim(c);
            identity.AddClaim(new Claim(ClaimTypes.Role, role.Value.ToString()));
            context.ShouldRenew = true;
        }
    }

    /// <summary>
    /// Maps the POS's non-page endpoints (staff sign-out). Call it after <c>UseAuthentication</c>,
    /// <c>UseAuthorization</c> and <c>UseAntiforgery</c>.
    /// </summary>
    public static IEndpointRouteBuilder MapQrboundPos(this IEndpointRouteBuilder endpoints)
    {
        CustomerSignIn.Map(endpoints);

        endpoints.MapPost("/pos/signout", async (HttpContext http) =>
        {
            await http.SignOutAsync(QrboundPosDefaults.AuthenticationScheme);
            return Results.LocalRedirect("~/pos/signin");
        });

        return endpoints;
    }

    /// <summary>
    /// Creates or upgrades the POS tables when <c>QrboundPos:ApplyMigrationsOnStartup</c> is on (the default).
    /// </summary>
    public static async Task InitializeQrboundPosDatabaseAsync(this IHost app)
    {
        var options = app.Services.GetRequiredService<IOptions<QrboundPosOptions>>().Value;
        if (!options.ApplyMigrationsOnStartup) return;

        var factory = app.Services.GetRequiredService<IDbContextFactory<PosDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Qrbound.POS");
        var database = db.Database.GetDbConnection().Database;
        logger.LogInformation("POS database: {Database} on {Server}", database, db.Database.GetDbConnection().DataSource);

        try
        {
            await db.Database.MigrateAsync();
        }
        catch (Microsoft.Data.SqlClient.SqlException ex) when (ex.Number is 18456 or 4060)
        {
            // SQL Server answers "login failed" / "cannot open database" for a database that doesn't
            // exist yet, which EF doesn't recognise as missing. Create it (through master), then migrate.
            // If it does exist ("already exists"), or the login can't reach master either (a real login
            // problem), the original error stands.
            var creator = db.GetService<Microsoft.EntityFrameworkCore.Storage.IRelationalDatabaseCreator>();
            try
            {
                await creator.CreateAsync();
            }
            catch (Microsoft.Data.SqlClient.SqlException create) when (create.Number is 1801 or 18456 or 262)
            {
                System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(ex);
            }

            logger.LogInformation("Created POS database {Database}", database);
            await db.Database.MigrateAsync();
        }
    }
}
