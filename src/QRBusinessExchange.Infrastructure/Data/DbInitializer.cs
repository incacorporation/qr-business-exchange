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

        // Check if data already exists
        if (await context.Users.AnyAsync())
        {
            return; // Database has been seeded
        }

        // Seed subscription types would go here if needed
        await context.SaveChangesAsync();
    }
}
