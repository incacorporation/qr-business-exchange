namespace QRBusinessExchange.Core.DTOs;

/// <summary>
/// DTO representing a user's profile information (excludes sensitive data like PasswordHash)
/// </summary>
public class UserProfileDto
{
    /// <summary>
    /// Unique identifier for the user
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// User's email address
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User's first name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// User's last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// User's company name
    /// </summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// User's phone number
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// URL to user's profile image
    /// </summary>
    public string? ProfileImageUrl { get; set; }

    /// <summary>
    /// User's biography/description
    /// </summary>
    public string? Bio { get; set; }

    /// <summary>
    /// JSON array of user's skills
    /// </summary>
    public string? Skills { get; set; }

    /// <summary>
    /// User's service pricing
    /// </summary>
    public decimal? ServicePricing { get; set; }

    /// <summary>
    /// Indicates if the user's email is verified
    /// </summary>
    public bool IsEmailVerified { get; set; }

    /// <summary>
    /// User's role (e.g., User, Admin, SuperAdmin)
    /// </summary>
    public string Role { get; set; } = "User";

    /// <summary>
    /// Indicates if the user account is active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Date and time when the user account was created
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Date and time when the user account was last updated
    /// </summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>
    /// Date and time of the user's last login
    /// </summary>
    public DateTime? LastLoginAt { get; set; }
}
