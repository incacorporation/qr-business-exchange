using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// A business (tenant) that uses the QR / SSO login core for its own applications.
/// </summary>
public class Business
{
    [Key]
    public Guid BusinessId { get; set; } = Guid.NewGuid();

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// URL-safe unique identifier (e.g. "acme-coffee").
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Slug { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    [StringLength(500)]
    public string? WebsiteUrl { get; set; }

    [Required]
    public Guid OwnerUserId { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual User Owner { get; set; } = null!;
    public virtual ICollection<BusinessMember> Members { get; set; } = new List<BusinessMember>();
    public virtual ICollection<SsoApplication> Applications { get; set; } = new List<SsoApplication>();
}

/// <summary>
/// Roles a user can hold inside a business.
/// </summary>
public static class BusinessRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Member = "Member";

    public static readonly string[] All = { Owner, Admin, Member };

    public static bool CanManage(string? role) => role is Owner or Admin;
}

/// <summary>
/// Membership of a user in a business. Membership is what lets a business
/// restrict SSO sign-in to its own staff/customers and carry a role into tokens.
/// </summary>
public class BusinessMember
{
    [Key]
    public Guid BusinessMemberId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid BusinessId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(50)]
    public string Role { get; set; } = BusinessRoles.Member;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Business Business { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
