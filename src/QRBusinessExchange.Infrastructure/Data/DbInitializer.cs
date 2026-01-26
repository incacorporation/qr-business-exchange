using QRBusinessExchange.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace QRBusinessExchange.Infrastructure.Data;

/// <summary>
/// Database initializer for seeding initial data
/// </summary>
public static class DbInitializer
{
    /// <summary>
    /// Seeds the database with initial data
    /// </summary>
    public static async Task Initialize(ApplicationDbContext context)
    {
        // Ensure database is created
        await context.Database.MigrateAsync();

        // Check if admin user already exists
        var adminEmail = "webmaster@qrbound.com";
        var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Email == adminEmail);
        
        if (existingAdmin == null)
        {
            // Create admin user
            var adminUser = new User
            {
                UserId = Guid.NewGuid(),
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("V-Md*%G@khh!0715"),
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
