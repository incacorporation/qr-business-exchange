using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace QRBusinessExchange.Core.DTOs;

// ---------------------------------------------------------------------------
// Businesses (tenants)
// ---------------------------------------------------------------------------

public class CreateBusinessRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional; generated from the name when omitted.</summary>
    [StringLength(100)]
    public string? Slug { get; set; }

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? WebsiteUrl { get; set; }

    [StringLength(500)]
    public string? LogoUrl { get; set; }
}

public class UpdateBusinessRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }

    [StringLength(500)]
    public string? WebsiteUrl { get; set; }

    [StringLength(500)]
    public string? LogoUrl { get; set; }

    public bool IsActive { get; set; } = true;
}

public class BusinessDto
{
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? WebsiteUrl { get; set; }
    public string? LogoUrl { get; set; }
    public bool IsActive { get; set; }
    public string MyRole { get; set; } = string.Empty;

    /// <summary>The signed-in user's named roles in this business (BusinessRoles), e.g. "Administrator".</summary>
    public List<string> MyRoles { get; set; } = new();

    public string? OwnerEmail { get; set; }
    public int ApplicationCount { get; set; }
    public int MemberCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>What the signed-in user can do with businesses.</summary>
public class MyBusinessAccessDto
{
    /// <summary>A SuperAdmin sees and manages every business.</summary>
    public bool IsSuperAdmin { get; set; }

    /// <summary>Number of businesses the user can open (all of them for a SuperAdmin).</summary>
    public int BusinessCount { get; set; }

    /// <summary>Number of businesses the user manages (Owner, Admin or Administrator; all for a SuperAdmin).</summary>
    public int ManagedCount { get; set; }
}

/// <summary>A named role a user holds in a business (BusinessRoles).</summary>
public class BusinessRoleDto
{
    public Guid BusinessRoleId { get; set; }
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AddBusinessRoleRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>"Administrator", or any other name of 2–50 letters, digits, spaces, '-' or '_'.</summary>
    [Required, StringLength(50, MinimumLength = 2)]
    public string RoleName { get; set; } = "Administrator";
}

public class BusinessMemberDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class AddBusinessMemberRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Role { get; set; } = "Member";
}

// ---------------------------------------------------------------------------
// SSO applications (clients)
// ---------------------------------------------------------------------------

public class CreateSsoApplicationRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Exact redirect URIs the application will send users back to.</summary>
    [Required, MinLength(1)]
    public List<string> RedirectUris { get; set; } = new();

    /// <summary>
    /// Confidential apps (with a backend) get a client secret.
    /// Public apps (SPA / mobile) have no secret and must use PKCE.
    /// </summary>
    public bool IsConfidential { get; set; } = true;

    public bool RequireMembership { get; set; } = false;

    /// <summary>Users need an approved category before they can sign in.</summary>
    public bool RequireApprovedCategory { get; set; }

    [Range(5, 1440)]
    public int TokenLifetimeMinutes { get; set; } = 60;
}

public class UpdateSsoApplicationRequest
{
    [Required, StringLength(200, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required, MinLength(1)]
    public List<string> RedirectUris { get; set; } = new();

    public bool RequireMembership { get; set; }

    public bool RequireApprovedCategory { get; set; }

    [Range(5, 1440)]
    public int TokenLifetimeMinutes { get; set; } = 60;

    public bool IsActive { get; set; } = true;
}

public class SsoApplicationDto
{
    public Guid SsoApplicationId { get; set; }
    public Guid BusinessId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public bool IsConfidential { get; set; }
    public List<string> RedirectUris { get; set; } = new();
    public bool RequireMembership { get; set; }
    public bool RequireApprovedCategory { get; set; }
    public int TokenLifetimeMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Only populated when the app is created or its secret is rotated.</summary>
    public string? ClientSecret { get; set; }
}

// ---------------------------------------------------------------------------
// SSO sign-in flow
// ---------------------------------------------------------------------------

/// <summary>
/// Public information about an application, shown on the sign-in page.
/// </summary>
public class SsoClientInfoDto
{
    public string ClientId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string BusinessName { get; set; } = string.Empty;
    public string? BusinessLogoUrl { get; set; }
    public string? BusinessWebsiteUrl { get; set; }
    public bool RequireMembership { get; set; }
    public bool IsConfidential { get; set; }
}

public class SsoAuthorizeRequest
{
    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string RedirectUri { get; set; } = string.Empty;

    public string? State { get; set; }

    public string? CodeChallenge { get; set; }

    /// <summary>Only "S256" is supported.</summary>
    public string? CodeChallengeMethod { get; set; }

    public string? Nonce { get; set; }

    /// <summary>Key of the application category to sign in as (e.g. "business"). Optional.</summary>
    public string? Category { get; set; }
}

public class SsoAuthorizeResponse
{
    public string RedirectUrl { get; set; } = string.Empty;
}

/// <summary>
/// Token response returned to business applications (OAuth-style snake_case).
/// </summary>
public class SsoTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("id_token")]
    public string IdToken { get; set; } = string.Empty;

    [JsonPropertyName("token_type")]
    public string TokenType { get; set; } = "Bearer";

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

public class SsoErrorResponse
{
    [JsonPropertyName("error")]
    public string Error { get; set; } = string.Empty;

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

// ---------------------------------------------------------------------------
// QR code login ("scan with your signed-in phone")
// ---------------------------------------------------------------------------

public class QrLoginCreateRequest
{
    /// <summary>Optional client_id of the application being signed in to.</summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Optional redirect URI the application will use to collect its code. When given (with
    /// <see cref="ClientId"/>), it must be registered for the application.
    /// </summary>
    public string? RedirectUri { get; set; }

    /// <summary>Key of the application category to sign in or register as (e.g. "business"). Optional.</summary>
    public string? Category { get; set; }
}

/// <summary>
/// An application collecting the result of its QR sign-in as an authorization code
/// (POST /api/qr-login/sessions/{id}/authorize). Never returns a portal session.
/// </summary>
public class QrLoginAuthorizeRequest
{
    [Required]
    public string PollSecret { get; set; } = string.Empty;

    [Required]
    public string ClientId { get; set; } = string.Empty;

    [Required]
    public string RedirectUri { get; set; } = string.Empty;

    public string? CodeChallenge { get; set; }

    /// <summary>Only "S256" is supported.</summary>
    public string? CodeChallengeMethod { get; set; }

    public string? Nonce { get; set; }

    public string? State { get; set; }
}

public class QrLoginAuthorizeResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    /// <summary>One-time authorization code; set exactly once, when the phone approved.</summary>
    public string? Code { get; set; }

    /// <summary>The state sent by the application, echoed with the code.</summary>
    public string? State { get; set; }

    /// <summary>Why no code was issued, for AccessPending and AccessDenied.</summary>
    public string? Message { get; set; }
}

public class QrLoginCreateResponse
{
    public Guid SessionId { get; set; }

    /// <summary>Secret only the requesting browser holds; required to collect the session.</summary>
    public string PollSecret { get; set; } = string.Empty;

    /// <summary>The URL encoded in the QR code.</summary>
    public string ApproveUrl { get; set; } = string.Empty;

    /// <summary>Number the user must pick on their phone.</summary>
    public string VerificationCode { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public int PollIntervalSeconds { get; set; } = 2;
}

public class QrLoginPollRequest
{
    [Required]
    public string PollSecret { get; set; } = string.Empty;
}

public class QrLoginPollResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }

    /// <summary>Populated exactly once, when the session has been approved.</summary>
    public AuthResponse? Auth { get; set; }
}

/// <summary>
/// What the approving phone sees about the pending sign-in.
/// </summary>
public class QrLoginSessionInfoDto
{
    public Guid SessionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ApplicationName { get; set; }
    public string? BusinessName { get; set; }
    public string? RequesterIp { get; set; }
    public string? RequesterUserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>Candidate numbers; the user picks the one shown on the other screen.</summary>
    public List<string> VerificationChoices { get; set; } = new();

    /// <summary>Set when the application asked for a category; tells the phone whether to request it.</summary>
    public SsoCategoryAccessDto? CategoryAccess { get; set; }
}

public class QrLoginApproveRequest
{
    [Required]
    public string VerificationCode { get; set; } = string.Empty;

    [Required]
    public string Challenge { get; set; } = string.Empty;

    [Required]
    public AssertionResponseDto Assertion { get; set; } = new();
}
