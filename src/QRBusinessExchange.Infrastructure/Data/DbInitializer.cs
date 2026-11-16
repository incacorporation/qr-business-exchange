using QRBusinessExchange.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace QRBusinessExchange.Infrastructure.Data;

/// <summary>
/// Database initializer for seeding initial data
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Seeds the database with initial data
    /// </summary>
    /// <remarks>
    /// Never changes the schema: the database is shared between development and production, so migrations
    /// are applied by hand (scripts/Run-Migrations.ps1 or dotnet ef database update), only once approved.
    /// Pending migrations are reported in the log instead.
    /// </remarks>
    public static async Task Initialize(ApplicationDbContext context, ILogger? logger = null, string? adminSeedPassword = null)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
        if (pending.Count > 0)
        {
            logger?.LogWarning(
                "The database is missing {Count} migration(s): {Migrations}. They are not applied automatically; " +
                "run them by hand once approved. Features that need them will fail until then.",
                pending.Count, string.Join(", ", pending));
        }

        // Check if admin user already exists
        var adminEmail = "webmaster@qrbound.com";
        var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
        
        if (existingAdmin == null && string.IsNullOrWhiteSpace(adminSeedPassword))
        {
            // No password is kept in code: set Admin:SeedPassword (user secrets or the Admin__SeedPassword
            // environment variable) only when a new, empty database needs its first administrator.
            logger?.LogInformation("No administrator account exists and Admin:SeedPassword is not set, so none was created.");
        }
        else if (existingAdmin == null)
        {
            // Create admin user
            var adminUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(adminSeedPassword!),
                FirstName = "System",
                LastName = "Administrator",
                CompanyName = "QR Bound",
                Role = "SuperAdmin",
                IsEmailVerified = true,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.Users.Add(adminUser);
            
            // Create initial subscription for admin user
            var adminSubscription = new Subscription
            {
                SubscriptionId = Guid.NewGuid(),
                UserId = adminUser.UserId,
                SubscriptionType = "Enterprise",
                CreditsAvailable = 999999,
                CreditsUsed = 0,
                StartDate = DateTime.UtcNow,
                EndDate = DateTime.UtcNow.AddYears(100),
                AutoRenew = false,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            context.Subscriptions.Add(adminSubscription);
            
            await context.SaveChangesAsync();
        }
    }
}
