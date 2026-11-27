using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// A kind of user an SSO application recognises, defined by the application's business:
/// for example "client", "business" or "vip". Approved categories are sent to the
/// application as <c>category</c> claims, so one qrbound user can be a "business" on one
/// site and a "client" on another.
/// </summary>
public class SsoAppCategory
{
    [Key]
    public Guid SsoAppCategoryId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SsoApplicationId { get; set; }

    /// <summary>Stable value sent in the <c>category</c> claim (lowercase letters, digits, '-' and '_').</summary>
    [Required]
    [StringLength(50)]
    public string Key { get; set; } = string.Empty;

    /// <summary>Shown to users ("Restaurant owner").</summary>
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>
    /// When true a request stays pending until an Owner or Admin of the application's business
    /// approves it; the user is emailed the decision. When false the category is granted at once.
    /// </summary>
    public bool RequiresApproval { get; set; } = true;

    /// <summary>
    /// Granted automatically on sign-in when no other category was requested.
    /// Only allowed for categories that don't require approval.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>
    /// The user signs in on behalf of one of their own businesses (where they are Owner or Admin),
    /// which is sent as the <c>org_*</c> claims.
    /// </summary>
    public bool RequiresBusiness { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public virtual SsoApplication Application { get; set; } = null!;
}

public static class SsoAccessStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Revoked = "Revoked";

    public static readonly string[] All = { Pending, Approved, Rejected, Revoked };
}

/// <summary>
/// A user's request for, or grant of, one category of an SSO application.
/// </summary>
public class SsoAppAccess
{
    [Key]
    public Guid SsoAppAccessId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid SsoApplicationId { get; set; }

    [Required]
    public Guid SsoAppCategoryId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    /// <summary>The user's own business they act for, when the category requires one.</summary>
    public Guid? BusinessId { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = SsoAccessStatus.Pending;

    /// <summary>Message from the user to the approvers.</summary>
    [StringLength(500)]
    public string? RequestNote { get; set; }

    /// <summary>Message from the approver to the user.</summary>
    [StringLength(500)]
    public string? DecisionNote { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DecidedAt { get; set; }

    public Guid? DecidedByUserId { get; set; }

    public virtual SsoApplication Application { get; set; } = null!;
    public virtual SsoAppCategory Category { get; set; } = null!;
    public virtual User User { get; set; } = null!;
    public virtual Business? Business { get; set; }
}
