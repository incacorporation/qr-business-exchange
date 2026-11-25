using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for the QR Business Exchange application
/// Inherits from IdentityDbContext to provide ASP.NET Core Identity support
/// </summary>
public class ApplicationDbContext : IdentityDbContext
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

    /// <summary>Businesses (tenants) using the SSO core</summary>
    public DbSet<Business> Businesses { get; set; } = null!;

    /// <summary>Business memberships</summary>
    public DbSet<BusinessMember> BusinessMembers { get; set; } = null!;

    /// <summary>Named roles users hold in a business (several per business and user)</summary>
    public DbSet<BusinessRole> BusinessRoles { get; set; } = null!;

    /// <summary>Applications registered for SSO</summary>
    public DbSet<SsoApplication> SsoApplications { get; set; } = null!;

    /// <summary>Single-use SSO authorization codes</summary>
    public DbSet<SsoAuthorizationCode> SsoAuthorizationCodes { get; set; } = null!;

    /// <summary>QR code login sessions</summary>
    public DbSet<QrLoginSession> QrLoginSessions { get; set; } = null!;

    public DbSet<SsoAppCategory> SsoAppCategories { get; set; } = null!;

    public DbSet<SsoAppAccess> SsoAppAccess { get; set; } = null!;

    /// <summary>People saved by scanning their business card</summary>
    public DbSet<Contact> Contacts { get; set; } = null!;

    /// <summary>Uploaded profile pictures and business logos</summary>
    public DbSet<ProfileImage> ProfileImages { get; set; } = null!;

    /// <summary>Card payments a business asks for with a QR code</summary>
    public DbSet<QrPaymentRequest> QrPaymentRequests { get; set; } = null!;

    /// <summary>
    /// Configures the database schema using Fluent API
    /// </summary>
    /// <param name="modelBuilder">The model builder to configure the schema</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure Identity table names (optional - customize as needed)
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUser>()
            .ToTable("AspNetUsers");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>()
            .ToTable("AspNetRoles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>()
            .ToTable("AspNetUserRoles");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>()
            .ToTable("AspNetUserClaims");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>()
            .ToTable("AspNetUserLogins");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>()
            .ToTable("AspNetUserTokens");
        modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>()
            .ToTable("AspNetRoleClaims");

        ConfigureUser(modelBuilder);
        ConfigureQRCode(modelBuilder);
        ConfigureConnection(modelBuilder);
        ConfigureContact(modelBuilder);
        ConfigureProfileImage(modelBuilder);
        ConfigureSubscription(modelBuilder);
        ConfigurePayment(modelBuilder);
        ConfigureAdvertisement(modelBuilder);
        ConfigureUserLocation(modelBuilder);
        ConfigurePasskeyCredential(modelBuilder);
        ConfigureEmailVerification(modelBuilder);
        ConfigureUsageAnalytics(modelBuilder);
        ConfigureSso(modelBuilder);
        ConfigureQrPaymentRequest(modelBuilder);
    }

    /// <summary>
    /// Configures QR payment requests
    /// </summary>
    private static void ConfigureQrPaymentRequest(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<QrPaymentRequest>(entity =>
        {
            entity.ToTable("QrPaymentRequests");
            entity.HasKey(e => e.QrPaymentRequestId);

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_QrPaymentRequests_Businesses");

            entity.HasIndex(e => e.InvoiceNumber)
                .IsUnique()
                .HasDatabaseName("IX_QrPaymentRequests_InvoiceNumber");

            entity.HasIndex(e => new { e.BusinessId, e.CreatedAt })
                .HasDatabaseName("IX_QrPaymentRequests_Business_Created");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }

    /// <summary>
    /// Configures the multi-business SSO / QR login entities
    /// </summary>
    private static void ConfigureSso(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Business>(entity =>
        {
            entity.ToTable("Businesses");
            entity.HasKey(e => e.BusinessId);

            entity.HasOne(e => e.Owner)
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Businesses_OwnerUser");

            entity.HasIndex(e => e.Slug)
                .IsUnique()
                .HasDatabaseName("IX_Businesses_Slug");

            entity.Property(e => e.BusinessId).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<BusinessMember>(entity =>
        {
            entity.ToTable("BusinessMembers");
            entity.HasKey(e => e.BusinessMemberId);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Members)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_BusinessMembers_Businesses");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_BusinessMembers_Users");

            entity.HasIndex(e => new { e.BusinessId, e.UserId })
                .IsUnique()
                .HasDatabaseName("IX_BusinessMembers_Business_User");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_BusinessMembers_UserId");

            entity.Property(e => e.BusinessMemberId).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<BusinessRole>(entity =>
        {
            // The trigger rejects SuperAdmin for anyone but BusinessRoleNames.SuperAdminEmail.
            entity.ToTable("BusinessRoles", tb => tb.HasTrigger("TR_BusinessRoles_SuperAdmin"));
            entity.HasKey(e => e.BusinessRoleId);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Roles)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_BusinessRoles_Businesses");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_BusinessRoles_Users");

            entity.HasIndex(e => new { e.BusinessId, e.UserId, e.RoleName })
                .IsUnique()
                .HasDatabaseName("IX_BusinessRoles_Business_User_Role");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_BusinessRoles_UserId");

            entity.Property(e => e.BusinessRoleId).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<SsoApplication>(entity =>
        {
            entity.ToTable("SsoApplications");
            entity.HasKey(e => e.SsoApplicationId);
            entity.Ignore(e => e.IsConfidential);

            entity.HasOne(e => e.Business)
                .WithMany(b => b.Applications)
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SsoApplications_Businesses");

            entity.HasIndex(e => e.ClientId)
                .IsUnique()
                .HasDatabaseName("IX_SsoApplications_ClientId");

            entity.HasIndex(e => e.BusinessId)
                .HasDatabaseName("IX_SsoApplications_BusinessId");

            entity.Property(e => e.SsoApplicationId).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            entity.Property(e => e.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<SsoAppCategory>(entity =>
        {
            entity.ToTable("SsoAppCategories");
            entity.HasKey(e => e.SsoAppCategoryId);

            entity.HasOne(e => e.Application)
                .WithMany(a => a.Categories)
                .HasForeignKey(e => e.SsoApplicationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SsoAppCategories_SsoApplications");

            entity.HasIndex(e => new { e.SsoApplicationId, e.Key })
                .IsUnique()
                .HasDatabaseName("IX_SsoAppCategories_Application_Key");

            entity.Property(e => e.SsoAppCategoryId).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        modelBuilder.Entity<SsoAppAccess>(entity =>
        {
            entity.ToTable("SsoAppAccess");
            entity.HasKey(e => e.SsoAppAccessId);

            // Cascades come through the category (deleting an app deletes its categories, which
            // deletes their access rows); SQL Server forbids a second cascade path from the app.
            entity.HasOne(e => e.Application)
                .WithMany()
                .HasForeignKey(e => e.SsoApplicationId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_SsoAppAccess_SsoApplications");

            entity.HasOne(e => e.Category)
                .WithMany()
                .HasForeignKey(e => e.SsoAppCategoryId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SsoAppAccess_SsoAppCategories");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_SsoAppAccess_Users");

            entity.HasOne(e => e.Business)
                .WithMany()
                .HasForeignKey(e => e.BusinessId)
                .OnDelete(DeleteBehavior.NoAction)
                .HasConstraintName("FK_SsoAppAccess_Businesses");

            entity.HasIndex(e => new { e.SsoAppCategoryId, e.UserId })
                .IsUnique()
                .HasDatabaseName("IX_SsoAppAccess_Category_User");

            entity.HasIndex(e => new { e.SsoApplicationId, e.Status })
                .HasDatabaseName("IX_SsoAppAccess_Application_Status");

            entity.HasIndex(e => e.UserId)
                .HasDatabaseName("IX_SsoAppAccess_UserId");

            entity.Property(e => e.SsoAppAccessId).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.RequestedAt).HasDefaultValueSql("GETUTCDATE()");

            // Two admins deciding the same request at once: only one wins
            entity.Property(e => e.Status).IsConcurrencyToken();
        });

        modelBuilder.Entity<SsoAuthorizationCode>(entity =>
        {
            entity.ToTable("SsoAuthorizationCodes");
            entity.HasKey(e => e.SsoAuthorizationCodeId);

            entity.HasOne(e => e.Application)
                .WithMany()
                .HasForeignKey(e => e.SsoApplicationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_SsoAuthorizationCodes_SsoApplications");

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_SsoAuthorizationCodes_Users");

            entity.HasIndex(e => e.CodeHash)
                .IsUnique()
                .HasDatabaseName("IX_SsoAuthorizationCodes_CodeHash");

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_SsoAuthorizationCodes_ExpiresAt");

            entity.Property(e => e.SsoAuthorizationCodeId).HasDefaultValueSql("NEWID()");
            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            // Single-use enforcement: a second exchange of the same code fails on save
            entity.Property(e => e.ConsumedAt).IsConcurrencyToken();
        });

        modelBuilder.Entity<QrLoginSession>(entity =>
        {
            entity.ToTable("QrLoginSessions");
            entity.HasKey(e => e.QrLoginSessionId);

            entity.HasOne(e => e.Application)
                .WithMany()
                .HasForeignKey(e => e.SsoApplicationId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_QrLoginSessions_SsoApplications");

            entity.HasOne(e => e.ApprovedUser)
                .WithMany()
                .HasForeignKey(e => e.ApprovedUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_QrLoginSessions_Users");

            entity.HasIndex(e => e.ExpiresAt)
                .HasDatabaseName("IX_QrLoginSessions_ExpiresAt");

            entity.Property(e => e.CreatedAt).HasDefaultValueSql("GETUTCDATE()");

            // Optimistic concurrency so two approvals / two polls can't both win
            entity.Property(e => e.Status).IsConcurrencyToken();
        });
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
    /// Configures the ProfileImage entity (one uploaded picture per user)
    /// </summary>
    private static void ConfigureProfileImage(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ProfileImage>(entity =>
        {
            entity.ToTable("ProfileImages");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.Data).HasColumnType("varbinary(max)");

            entity.HasOne(e => e.User)
                .WithOne()
                .HasForeignKey<ProfileImage>(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_ProfileImages_Users");
        });
    }

    /// <summary>
    /// Configures the Contact entity (business cards a user has saved)
    /// </summary>
    private static void ConfigureContact(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Contact>(entity =>
        {
            entity.ToTable("Contacts");
            entity.HasKey(e => e.ContactId);

            entity.HasOne(e => e.OwnerUser)
                .WithMany()
                .HasForeignKey(e => e.OwnerUserId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_Contacts_OwnerUser");

            // Restrict: SQL Server allows only one cascade path to Users.
            entity.HasOne(e => e.ContactUser)
                .WithMany()
                .HasForeignKey(e => e.ContactUserId)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_Contacts_ContactUser");

            // One entry per person per list; also serves "my contacts" lookups.
            entity.HasIndex(e => new { e.OwnerUserId, e.ContactUserId })
                .IsUnique()
                .HasDatabaseName("IX_Contacts_Owner_Contact");

            entity.HasIndex(e => e.ContactUserId)
                .HasDatabaseName("IX_Contacts_ContactUserId");
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
                .HasPrecision(10, 8);

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

            // Note: Spatial index for GeographyPoint column must be created via raw SQL in migrations
            // Cannot use HasIndex() for geography/geometry types

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
                .HasPrecision(11, 8); // -180 to 180 needs three whole digits
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
