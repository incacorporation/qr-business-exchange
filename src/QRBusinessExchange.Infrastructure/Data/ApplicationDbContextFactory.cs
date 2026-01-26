using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QRBusinessExchange.Infrastructure.Data;

/// <summary>
/// Design-time factory for creating ApplicationDbContext instances
/// Used by Entity Framework Core tools for migrations
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    /// <summary>
    /// Creates a new instance of ApplicationDbContext for design-time operations
    /// </summary>
    /// <param name="args">Command-line arguments</param>
    /// <returns>A configured ApplicationDbContext instance</returns>
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        
        // Use the same connection string as in the API project
        optionsBuilder.UseSqlServer(
            "Server=incacorporation.net;Database=www.qrbound.com;User Id=ssoAdministrator;Password=8^7lhy1F;TrustServerCertificate=True;Encrypt=False;",
            b => b.MigrationsAssembly("QRBusinessExchange.Infrastructure")
        );

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
