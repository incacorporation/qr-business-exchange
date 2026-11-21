using Microsoft.EntityFrameworkCore;
using mlm.qrbound.com.Domain;

namespace mlm.qrbound.com.Data;

public class MlmDbContext(DbContextOptions<MlmDbContext> options) : DbContext(options)
{
    public DbSet<Member> Members => Set<Member>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<CommissionRun> CommissionRuns => Set<CommissionRun>();
    public DbSet<CommissionLine> CommissionLines => Set<CommissionLine>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Member>(e =>
        {
            e.HasIndex(m => m.Slug).IsUnique();
            e.HasIndex(m => m.Email).IsUnique();
            e.HasOne(m => m.Sponsor).WithMany(m => m.Frontline).HasForeignKey(m => m.SponsorId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Product>().HasIndex(p => p.Slug).IsUnique();
        b.Entity<Order>().HasIndex(o => o.PlacedUtc);
        b.Entity<Lead>().HasIndex(l => l.Email);
        b.Entity<CommissionRun>().HasIndex(r => r.Period).IsUnique();

        // SQLite stores decimals as TEXT, which keeps money exact but can't be summed or sorted in SQL.
        // Queries therefore load rows and aggregate money in memory.
    }
}
