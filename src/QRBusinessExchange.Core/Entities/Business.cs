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
    public virtual ICollection<BusinessRole> Roles { get; set; } = new List<BusinessRole>();
}

/// <summary>
/// Names of the roles kept in the BusinessRoles table.
/// </summary>
public static class BusinessRoleNames
{
    /// <summary>Given to the user who creates a business.</summary>
    public const string Administrator = "Administrator";

    /// <summary>Only <see cref="SuperAdminEmail"/> can hold this role (also enforced by a database trigger).</summary>
    public const string SuperAdmin = "SuperAdmin";

    public const string SuperAdminEmail = "gmacha@incacorporation.com";
}

/// <summary>
/// A named role a user holds in a business. A business has many roles, and a user can hold
/// several of them in the same business.
/// </summary>
public class BusinessRole
{
    [Key]
    public Guid BusinessRoleId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid BusinessId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(50)]
    public string RoleName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual Business Business { get; set; } = null!;
    public virtual User User { get; set; } = null!;
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
