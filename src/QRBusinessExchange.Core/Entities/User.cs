using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// Represents a user in the QR Business Exchange system
/// </summary>
public class User
{
    [Key]
    public Guid UserId { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(255)]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    public string LastName { get; set; } = string.Empty;

    [StringLength(255)]
    public string? CompanyName { get; set; }

    [StringLength(20)]
    public string? PhoneNumber { get; set; }

    [StringLength(500)]
    public string? ProfileImageUrl { get; set; }

    public string? Bio { get; set; }

    /// <summary>
    /// JSON array of skills
    /// </summary>
    public string? Skills { get; set; }

    public decimal? ServicePricing { get; set; }

    // What people who open this user's business card (public profile) or have them in their contacts may see.
    // The name is always shown. All default to shown, which is how profiles behaved before these existed.
    public bool ShowEmail { get; set; } = true;
    public bool ShowCompanyName { get; set; } = true;
    public bool ShowPhoneNumber { get; set; } = true;
    public bool ShowBio { get; set; } = true;
    public bool ShowSkills { get; set; } = true;
    public bool ShowServicePricing { get; set; } = true;
    public bool ShowProfileImage { get; set; } = true;

    public bool IsEmailVerified { get; set; } = false;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// User role: "User", "Admin", "SuperAdmin"
    /// </summary>
    [StringLength(50)]
    public string Role { get; set; } = "User";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public virtual ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    public virtual ICollection<UserLocation> UserLocations { get; set; } = new List<UserLocation>();
    public virtual ICollection<Connection> InitiatedConnections { get; set; } = new List<Connection>();
    public virtual ICollection<Connection> ReceivedConnections { get; set; } = new List<Connection>();
    public virtual ICollection<Payment> Payments { get; set; } = new List<Payment>();
    public virtual ICollection<Advertisement> Advertisements { get; set; } = new List<Advertisement>();
    public virtual ICollection<QRCode> QRCodes { get; set; } = new List<QRCode>();
    public virtual ICollection<PasskeyCredential> PasskeyCredentials { get; set; } = new List<PasskeyCredential>();
    public virtual ICollection<EmailVerification> EmailVerifications { get; set; } = new List<EmailVerification>();
    public virtual ICollection<UsageAnalytics> UsageAnalytics { get; set; } = new List<UsageAnalytics>();
}
