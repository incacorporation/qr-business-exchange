using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the QR Business Exchange application
/// </summary>
public class ApplicationDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ApplicationDbContext"/> class
    /// </summary>
    /// <param name="options">The options to be used by the DbContext</param>
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Gets or sets the Users DbSet
    /// </summary>
    public DbSet<User> Users { get; set; } = null!;

    /// <summary>
    /// Gets or sets the QRCodes DbSet
    /// </summary>
    public DbSet<QRCode> QRCodes { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Connections DbSet
    /// </summary>
    public DbSet<Connection> Connections { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Subscriptions DbSet
    /// </summary>
    public DbSet<Subscription> Subscriptions { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Payments DbSet
    /// </summary>
    public DbSet<Payment> Payments { get; set; } = null!;

    /// <summary>
    /// Gets or sets the Advertisements DbSet
    /// </summary>
    public DbSet<Advertisement> Advertisements { get; set; } = null!;

    /// <summary>
    /// Gets or sets the UserLocations DbSet
    /// </summary>
    public DbSet<UserLocation> UserLocations { get; set; } = null!;

    /// <summary>
    /// Gets or sets the PasskeyCredentials DbSet
    /// </summary>
    public DbSet<PasskeyCredential> PasskeyCredentials { get; set; } = null!;

    /// <summary>
    /// Gets or sets the EmailVerifications DbSet
    /// </summary>
    public DbSet<EmailVerification> EmailVerifications { get; set; } = null!;

    /// <summary>
    /// Gets or sets the UsageAnalytics DbSet
    /// </summary>
    public DbSet<UsageAnalytics> UsageAnalytics { get; set; } = null!;

    /// <summary>
    /// Configures the database schema using Fluent API
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure the schema</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        ConfigureUser(modelBuilder);
        ConfigureQRCode(modelBuilder);
        ConfigureConnection(modelBuilder);
        ConfigureSubscription(modelBuilder);
        ConfigurePayment(modelBuilder);
        ConfigureAdvertisement(modelBuilder);
        ConfigureUserLocation(modelBuilder);
        ConfigurePasskeyCredential(modelBuilder);
        ConfigureEmailVerification(modelBuilder);
        ConfigureUsageAnalytics(modelBuilder);
    }

    /// <summary>
    /// Configures the User entity
    /// </summary>
    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.UserId);

            // Indexes
            entity.HasIndex(e => e.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_Users_IsActive");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_Users_CreatedAt");

            // Default values
            entity.Property(e => e.UserId)
                .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.IsEmailVerified)
                .HasDefaultValue(false);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    /// <summary>
    /// Configures the QRCode entity
    /// </summary>
    private static void ConfigureQRCode(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QRCode>(entity =>
        {
            entity.ToTable("QRCodes");
            entity.HasKey(e => e.QRCodeId);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany(u => u.QRCodes)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_QRCodes_Users");

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_QRCodes_UserId");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_QRCodes_CreatedAt");

            // Default values
            entity.Property(e => e.QRCodeId)
                .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.ScansCount)
                .HasDefaultValue(0);
        });
    }

    /// <summary>
    /// Configures the Connection entity
    /// </summary>
    private static void ConfigureConnection(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Connection>(entity =>
        {
            entity.ToTable("Connections");
            entity.HasKey(e => e.ConnectionId);

            // Relationships
            entity.HasOne(e => e.InitiatorUser)
                .WithMany(u => u.InitiatedConnections)
                .HasForeignKey(e => e.InitiatorUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Connections_InitiatorUser");

            entity.HasOne(e => e.RecipientUser)
                .WithMany(u => u.ReceivedConnections)
                .HasForeignKey(e => e.RecipientUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Connections_RecipientUser");

            // Indexes
            entity.HasIndex(e => e.InitiatorUserId)
                .HasDatabaseName("IX_Connections_InitiatorUserId");

            entity.HasIndex(e => e.RecipientUserId)
                .HasDatabaseName("IX_Connections_RecipientUserId");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_Connections_Status");

            entity.HasIndex(e => e.ConnectionDate)
                .HasDatabaseName("IX_Connections_ConnectionDate");

            // Composite index for finding connections between two users
            entity.HasIndex(e => new { e.InitiatorUserId, e.RecipientUserId, e.Status })
                .HasDatabaseName("IX_Connections_Users_Status");

            // Default values
            entity.Property(e => e.ConnectionId)
                .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.Status)
                .HasDefaultValue("Pending");

            entity.Property(e => e.CreditDeducted)
                .HasDefaultValue(false);

            entity.Property(e => e.ConnectionDate)
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    /// <summary>
    /// Configures the Subscription entity
    /// </summary>
    private static void ConfigureSubscription(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Subscription>(entity =>
        {
            entity.ToTable("Subscriptions");
            entity.HasKey(e => e.SubscriptionId);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Subscriptions_Users");

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Subscriptions_UserId");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_Subscriptions_IsActive");

            entity.HasIndex(e => new { e.UserId, e.IsActive })
                .HasDatabaseName("IX_Subscriptions_UserId_IsActive");

            entity.HasIndex(e => e.EndDate)
                .HasDatabaseName("IX_Subscriptions_EndDate");

            // Default values
            entity.Property(e => e.SubscriptionId)
                .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.SubscriptionType)
                .HasDefaultValue("Free");

            entity.Property(e => e.CreditsAvailable)
                .HasDefaultValue(0);

            entity.Property(e => e.CreditsUsed)
                .HasDefaultValue(0);

            entity.Property(e => e.StartDate)
                .HasDefaultValueSql("GETUTCDATE()");

            entity.Property(e => e.AutoRenew)
                .HasDefaultValue(true);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    /// <summary>
    /// Configures the Payment entity
    /// </summary>
    private static void ConfigurePayment(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Payment>(entity =>
        {
            entity.ToTable("Payments");
            entity.HasKey(e => e.PaymentId);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany(u => u.Payments)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Payments_Users");

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Payments_UserId");

            entity.HasIndex(e => e.Status)
                .HasDatabaseName("IX_Payments_Status");

            entity.HasIndex(e => e.TransactionId)
                .HasDatabaseName("IX_Payments_TransactionId");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_Payments_CreatedAt");

            entity.HasIndex(e => new { e.UserId, e.Status })
                .HasDatabaseName("IX_Payments_UserId_Status");

            // Default values
            entity.Property(e => e.PaymentId)
                .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.Currency)
                .HasDefaultValue("USD");

            entity.Property(e => e.Status)
                .HasDefaultValue("Pending");

            entity.Property(e => e.IsVerified)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Precision for decimal columns
            entity.Property(e => e.Amount)
                .HasPrecision(10, 2);

            entity.Property(e => e.PaymentLatitude)
                .HasPrecision(10, 8);

            entity.Property(e => e.PaymentLongitude)
                .HasPrecision(10, 8);

            entity.Property(e => e.VerificationLatitude)
                .HasPrecision(10, 8);

            entity.Property(e => e.VerificationLongitude)
                .HasPrecision(11, 8);

            entity.Property(e => e.DistanceVerification)
                .HasPrecision(10, 2);
        });
    }

    /// <summary>
    /// Configures the Advertisement entity
    /// </summary>
    private static void ConfigureAdvertisement(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Advertisement>(entity =>
        {
            entity.ToTable("Advertisements");
            entity.HasKey(e => e.AdId);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany(u => u.Advertisements)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Advertisements_Users");

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_Advertisements_UserId");

            entity.HasIndex(e => e.IsActive)
                .HasDatabaseName("IX_Advertisements_IsActive");

            entity.HasIndex(e => new { e.StartDate, e.EndDate })
                .HasDatabaseName("IX_Advertisements_DateRange");

            entity.HasIndex(e => new { e.IsActive, e.StartDate, e.EndDate })
                .HasDatabaseName("IX_Advertisements_Active_DateRange");

            // Default values
            entity.Property(e => e.AdId)
                .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.CostPerDay)
                .HasDefaultValue(5.00m);

            entity.Property(e => e.IsActive)
                .HasDefaultValue(true);

            entity.Property(e => e.Impressions)
                .HasDefaultValue(0);

            entity.Property(e => e.Clicks)
                .HasDefaultValue(0);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");

            // Precision for decimal columns
            entity.Property(e => e.CostPerDay)
                .HasPrecision(10, 2);

            entity.Property(e => e.TotalCost)
                .HasPrecision(10, 2);
        });
    }

    /// <summary>
    /// Configures the UserLocation entity
    /// </summary>
    private static void ConfigureUserLocation(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserLocation>(entity =>
        {
            entity.ToTable("UserLocations");
            entity.HasKey(e => e.LocationId);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany(u => u.UserLocations)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UserLocations_Users");

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_UserLocations_UserId");

            entity.HasIndex(e => e.PingMeEnabled)
                .HasDatabaseName("IX_UserLocations_PingMeEnabled");

            entity.HasIndex(e => e.LastUpdated)
                .HasDatabaseName("IX_UserLocations_LastUpdated");

            // Spatial index for geography column
            entity.HasIndex(e => e.GeographyPoint)
                .HasDatabaseName("IX_UserLocations_GeographyPoint");

            // Default values
            entity.Property(e => e.LocationId)
                .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.PingMeEnabled)
                .HasDefaultValue(false);

            entity.Property(e => e.LastUpdated)
                .HasDefaultValueSql("GETUTCDATE()");

            // Precision for decimal columns
            entity.Property(e => e.Latitude)
                .HasPrecision(10, 8);

            entity.Property(e => e.Longitude)
                .HasPrecision(11, 8);
        });
    }

    /// <summary>
    /// Configures the PasskeyCredential entity
    /// </summary>
    private static void ConfigurePasskeyCredential(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PasskeyCredential>(entity =>
        {
            entity.ToTable("PasskeyCredentials");
            entity.HasKey(e => e.CredentialId);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany(u => u.PasskeyCredentials)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_PasskeyCredentials_Users");

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_PasskeyCredentials_UserId");

            entity.HasIndex(e => e.CreatedAt)
                .HasDatabaseName("IX_PasskeyCredentials_CreatedAt");

            // Default values
            entity.Property(e => e.Counter)
                .HasDefaultValue(0);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    /// <summary>
    /// Configures the EmailVerification entity
    /// </summary>
    private static void ConfigureEmailVerification(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<EmailVerification>(entity =>
        {
            entity.ToTable("EmailVerifications");
            entity.HasKey(e => e.VerificationId);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany(u => u.EmailVerifications)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_EmailVerifications_Users");

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_EmailVerifications_UserId");

            entity.HasIndex(e => e.VerificationCode)
                .HasDatabaseName("IX_EmailVerifications_VerificationCode");

            entity.HasIndex(e => new { e.UserId, e.IsUsed, e.ExpiresAt })
                .HasDatabaseName("IX_EmailVerifications_Validation");

            // Default values
            entity.Property(e => e.VerificationId)
                .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.IsUsed)
                .HasDefaultValue(false);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("GETUTCDATE()");
        });
    }

    /// <summary>
    /// Configures the UsageAnalytics entity
    /// </summary>
    private static void ConfigureUsageAnalytics(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UsageAnalytics>(entity =>
        {
            entity.ToTable("UsageAnalytics");
            entity.HasKey(e => e.AnalyticsId);

            // Relationships
            entity.HasOne(e => e.User)
                .WithMany(u => u.UsageAnalytics)
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_UsageAnalytics_Users");

            // Indexes
            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_UsageAnalytics_UserId");

            entity.HasIndex(e => e.Date)
                .HasDatabaseName("IX_UsageAnalytics_Date");

            // Unique index to ensure one record per user per day
            entity.HasIndex(e => new { e.UserId, e.Date })
                .IsUnique()
                .HasDatabaseName("IX_UsageAnalytics_UserId_Date");

            // Default values
            entity.Property(e => e.AnalyticsId)
                .HasDefaultValueSql("NEWID()");

            entity.Property(e => e.ConnectionsMade)
                .HasDefaultValue(0);

            entity.Property(e => e.QRScans)
                .HasDefaultValue(0);

            entity.Property(e => e.ProfileViews)
                .HasDefaultValue(0);

            entity.Property(e => e.MapViews)
                .HasDefaultValue(0);
        });
    }
}
