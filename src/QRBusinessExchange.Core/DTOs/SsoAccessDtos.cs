using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.DTOs;

// ---------------------------------------------------------------------------
// Application categories ("client", "business", "vip", ...) and who holds them
// ---------------------------------------------------------------------------

public class SsoAppCategoryDto
{
    public Guid SsoAppCategoryId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequiresApproval { get; set; }
    public bool IsDefault { get; set; }
    public bool RequiresBusiness { get; set; }
    public int SortOrder { get; set; }
}

public class SaveSsoAppCategoryRequest
{
    /// <summary>Value of the <c>category</c> claim: lowercase letters, digits, '-' and '_'.</summary>
    [Required, StringLength(50, MinimumLength = 2)]
    [RegularExpression("^[a-z0-9][a-z0-9_-]*$", ErrorMessage = "Use lowercase letters, digits, '-' and '_'")]
    public string Key { get; set; } = string.Empty;

    [Required, StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public bool RequiresApproval { get; set; } = true;

    public bool IsDefault { get; set; }

    public bool RequiresBusiness { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>A user's request for, or grant of, a category; seen by the app's administrators.</summary>
public class SsoAppAccessDto
{
    public Guid SsoAppAccessId { get; set; }
    public Guid UserId { get; set; }
    public string UserEmail { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string CategoryKey { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public Guid? BusinessId { get; set; }
    public string? BusinessName { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? RequestNote { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class SsoAccessDecisionRequest
{
    [StringLength(500)]
    public string? Note { get; set; }
}

/// <summary>The signed-in user asks for a category of an application.</summary>
public class SsoAccessRequest
{
    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required, StringLength(50)]
    public string Category { get; set; } = string.Empty;

    /// <summary>One of the user's businesses (Owner or Admin); required when the category acts for a business.</summary>
    public Guid? BusinessId { get; set; }

    [StringLength(500)]
    public string? Note { get; set; }
}

/// <summary>Where the signed-in user stands with one category of an application.</summary>
public class SsoCategoryAccessDto
{
    public string ClientId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string CategoryKey { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool RequiresApproval { get; set; }
    public bool RequiresBusiness { get; set; }

    /// <summary>Null when the user never asked; otherwise Pending, Approved, Rejected or Revoked.</summary>
    public string? Status { get; set; }

    public Guid? BusinessId { get; set; }
    public string? BusinessName { get; set; }
    public string? DecisionNote { get; set; }

    /// <summary>The user's businesses they may act for (Owner or Admin), when the category requires one.</summary>
    public List<SsoEligibleBusinessDto> EligibleBusinesses { get; set; } = new();
}

public class SsoEligibleBusinessDto
{
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
