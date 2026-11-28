using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Qrbound.POS.Data;

/// <summary>
/// The POS tables, all in the <c>pos</c> schema, so they can live in a database of their own or in
/// one the host app already uses. Which database is configuration: see <see cref="QrboundPosOptions"/>.
/// </summary>
public class PosDbContext(DbContextOptions<PosDbContext> options) : DbContext(options)
{
    public const string Schema = "pos";

    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<MenuCategory> MenuCategories => Set<MenuCategory>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<ModifierGroup> ModifierGroups => Set<ModifierGroup>();
    public DbSet<Modifier> Modifiers => Set<Modifier>();
    public DbSet<DiningTable> Tables => Set<DiningTable>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<StaffMember> Staff => Set<StaffMember>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.HasDefaultSchema(Schema);

        b.Entity<Restaurant>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Slug).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.LogoUrl).HasMaxLength(500);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.TaxRatePercent).HasPrecision(6, 3);
            e.Property(x => x.TimeZoneId).HasMaxLength(64).IsRequired();
        });

        b.Entity<MenuCategory>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.Property(x => x.Description).HasMaxLength(300);
            e.HasOne(x => x.Restaurant).WithMany(r => r.Categories).HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RestaurantId, x.SortOrder });
        });

        b.Entity<MenuItem>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Description).HasMaxLength(500);
            e.Property(x => x.ImageUrl).HasMaxLength(500);
            // RestaurantId is denormalised for queries; the category owns the item.
            e.HasOne(x => x.Category).WithMany(c => c.Items).HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.RestaurantId, x.CategoryId, x.SortOrder });
        });

        b.Entity<ModifierGroup>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasOne(x => x.MenuItem).WithMany(i => i.ModifierGroups).HasForeignKey(x => x.MenuItemId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Modifier>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80).IsRequired();
            e.HasOne(x => x.Group).WithMany(g => g.Modifiers).HasForeignKey(x => x.ModifierGroupId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<DiningTable>(e =>
        {
            e.ToTable("DiningTables");
            e.Property(x => x.Name).HasMaxLength(60).IsRequired();
            e.Property(x => x.Code).HasMaxLength(32).IsRequired();
            e.HasIndex(x => new { x.RestaurantId, x.Code }).IsUnique();
            e.HasOne(x => x.Restaurant).WithMany(r => r.Tables).HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Order>(e =>
        {
            e.Property(x => x.PublicToken).HasMaxLength(32).IsRequired();
            e.HasIndex(x => x.PublicToken).IsUnique();
            e.HasIndex(x => new { x.RestaurantId, x.BusinessDate, x.Number }).IsUnique();
            e.HasIndex(x => new { x.RestaurantId, x.Status });
            e.HasIndex(x => x.QrPaymentId);
            e.HasIndex(x => x.PaymentStatus);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PaymentMethod).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CustomerName).HasMaxLength(80);
            e.Property(x => x.CustomerUserId).HasMaxLength(64);
            e.Property(x => x.CustomerEmail).HasMaxLength(254);
            e.HasIndex(x => x.CustomerUserId);
            e.Property(x => x.Notes).HasMaxLength(500);
            e.Property(x => x.TaxRatePercent).HasPrecision(6, 3);
            e.Property(x => x.Currency).HasMaxLength(3).IsRequired();
            e.Property(x => x.PaymentInvoiceNumber).HasMaxLength(30);
            e.Property(x => x.TransactionId).HasMaxLength(30);
            e.Property(x => x.PaymentError).HasMaxLength(300);
            e.Property(x => x.CreatedBy).HasMaxLength(80);
            e.Property(x => x.RowVersion).IsRowVersion();
            e.HasOne(x => x.Restaurant).WithMany().HasForeignKey(x => x.RestaurantId).OnDelete(DeleteBehavior.Restrict);
            // A table can be deleted; its past orders stay, without the table.
            e.HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableId).OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<OrderLine>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.ModifiersText).HasMaxLength(500);
            e.Property(x => x.Notes).HasMaxLength(200);
            e.HasOne(x => x.Order).WithMany(o => o.Lines).HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<StaffMember>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(60).IsRequired();
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(80).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(20);
        });
    }
}

/// <summary>Lets <c>dotnet ef migrations add</c> build the context without a host app.</summary>
internal sealed class PosDbContextDesignTimeFactory : IDesignTimeDbContextFactory<PosDbContext>
{
    public PosDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=QrboundPos_Design;Trusted_Connection=True",
                sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", PosDbContext.Schema))
            .Options);
}
