using System.Text.Json.Serialization;

namespace Qrbound.SignIn;

/// <summary>What the QR code is for.</summary>
public enum QrboundMode
{
    /// <summary>The user already has a qrbound account on their phone.</summary>
    SignIn,

    /// <summary>
    /// The user may not have an account yet: the phone is offered "Create a qrbound account" first,
    /// then approves the sign-in.
    /// </summary>
    Register
}

/// <summary>Status values of a QR sign-in request.</summary>
public static class QrboundStatus
{
    public const string Pending = "Pending";
    public const string Scanned = "Scanned";
    public const string Approved = "Approved";
    public const string Denied = "Denied";
    public const string Consumed = "Consumed";
    public const string Expired = "Expired";

    /// <summary>
    /// The phone approved, and the user asked for a category of your app that needs approval: your app's
    /// administrators were emailed, and the user is emailed once they decide. No code is issued.
    /// </summary>
    public const string AccessPending = "AccessPending";

    /// <summary>The user may not use your app (as that category): rejected, revoked or never requested.</summary>
    public const string AccessDenied = "AccessDenied";

    /// <summary>True when the request can no longer be approved.</summary>
    public static bool IsFinal(string status) =>
        status is Approved or Denied or Consumed or Expired or AccessPending or AccessDenied;
}

/// <summary>
/// A pending QR sign-in. The PKCE verifier and poll secret stay in this object and never leave the app.
/// </summary>
public sealed class QrboundQrSession
{
    public Guid SessionId { get; init; }

    /// <summary>The URL encoded in the QR code (opened on the user's phone).</summary>
    public string ApproveUrl { get; init; } = string.Empty;

    /// <summary>The number the user picks on their phone. Show it next to the QR code.</summary>
    public string VerificationCode { get; init; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; init; }

    public TimeSpan PollInterval { get; init; }

    public QrboundMode Mode { get; init; }

    /// <summary>The category of your app the user is signing in as (e.g. "business"), if one was asked for.</summary>
    public string? Category { get; init; }

    internal string PollSecret { get; init; } = string.Empty;
    internal string CodeVerifier { get; init; } = string.Empty;
    internal string CodeChallenge { get; init; } = string.Empty;
    internal string Nonce { get; init; } = string.Empty;
    internal string State { get; init; } = string.Empty;
}

/// <summary>Result of one poll.</summary>
public sealed class QrboundPollResult
{
    public string Status { get; init; } = QrboundStatus.Pending;
    public DateTimeOffset ExpiresAt { get; init; }

    /// <summary>The one-time authorization code; set only when <see cref="Status"/> is Approved.</summary>
    public string? Code { get; init; }

    /// <summary>Why no code was issued, for AccessPending and AccessDenied; safe to show to the user.</summary>
    public string? Message { get; init; }
}

/// <summary>A completed sign-in.</summary>
public sealed class QrboundSignInResult
{
    public QrboundUser User { get; init; } = new();

    /// <summary>
    /// Tokens for the app, when the browser exchanged the code itself (<see cref="QrboundTokenExchange.Direct"/>).
    /// Null in backend mode: the tokens stay on your server.
    /// </summary>
    public QrboundTokenResponse? Tokens { get; init; }

    /// <summary>Where to go after a redirect sign-in (the returnUrl given when it started).</summary>
    public string? ReturnUrl { get; init; }
}

/// <summary>An error reported by qrbound (OAuth error code plus description).</summary>
public sealed class QrboundException(string error, string? description, int? statusCode = null)
    : Exception(description ?? error)
{
    public string Error { get; } = error;
    public int? StatusCode { get; } = statusCode;
}

// ------------------------------------------------------------------ wire formats

internal sealed class QrSessionCreateRequest
{
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string? Category { get; set; }
}

internal sealed class QrSessionCreateResponse
{
    public Guid SessionId { get; set; }
    public string PollSecret { get; set; } = string.Empty;
    public string ApproveUrl { get; set; } = string.Empty;
    public string VerificationCode { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public int PollIntervalSeconds { get; set; }
}

internal sealed class QrSessionAuthorizeRequest
{
    public string PollSecret { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string CodeChallenge { get; set; } = string.Empty;
    public string CodeChallengeMethod { get; set; } = Pkce.MethodS256;
    public string? Nonce { get; set; }
    public string? State { get; set; }
}

internal sealed class QrSessionAuthorizeResponse
{
    public string Status { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string? Code { get; set; }
    public string? State { get; set; }
    public string? Message { get; set; }
}

internal sealed class OAuthError
{
    [JsonPropertyName("error")] public string? Error { get; set; }
    [JsonPropertyName("error_description")] public string? ErrorDescription { get; set; }
}

/// <summary>
/// Body the browser posts to your backend in <see cref="QrboundTokenExchange.Backend"/> mode.
/// </summary>
public sealed class QrboundBackendExchangeRequest
{
    public string Code { get; set; } = string.Empty;
    public string CodeVerifier { get; set; } = string.Empty;
    public string? Nonce { get; set; }
}

/// <summary>The user your backend returns after it exchanged the code.</summary>
public sealed class QrboundBackendUser
{
    public string Sub { get; set; } = string.Empty;
    public string? Email { get; set; }
    public bool EmailVerified { get; set; }
    public string? Name { get; set; }
    public string? GivenName { get; set; }
    public string? FamilyName { get; set; }
    public string? AuthMethod { get; set; }
    public Guid? BusinessId { get; set; }
    public string? BusinessSlug { get; set; }
    public string? BusinessRole { get; set; }
    public List<string> Categories { get; set; } = new();
    public Guid? OrganizationId { get; set; }
    public string? OrganizationSlug { get; set; }
    public string? OrganizationName { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public static QrboundBackendUser From(QrboundUser u) => new()
    {
        Sub = u.Subject, Email = u.Email, EmailVerified = u.EmailVerified, Name = u.Name,
        GivenName = u.GivenName, FamilyName = u.FamilyName, AuthMethod = u.AuthMethod,
        BusinessId = u.BusinessId, BusinessSlug = u.BusinessSlug, BusinessRole = u.BusinessRole,
        Categories = u.Categories.ToList(), OrganizationId = u.Organization?.Id,
        OrganizationSlug = u.Organization?.Slug, OrganizationName = u.Organization?.Name,
        ExpiresAt = u.ExpiresAt
    };

    public QrboundUser ToUser()
    {
        var claims = new List<System.Security.Claims.Claim> { new("sub", Sub) };
        void Add(string type, string? value) { if (!string.IsNullOrEmpty(value)) claims.Add(new(type, value)); }
        Add("email", Email); Add("name", Name); Add("given_name", GivenName); Add("family_name", FamilyName);
        Add("amr", AuthMethod); Add("business_id", BusinessId?.ToString()); Add("business_slug", BusinessSlug);
        Add("business_role", BusinessRole);
        foreach (var category in Categories) Add(QrboundClaims.Category, category);
        Add(QrboundClaims.OrganizationId, OrganizationId?.ToString()); Add(QrboundClaims.OrganizationSlug, OrganizationSlug);
        Add(QrboundClaims.OrganizationName, OrganizationName);
        return new QrboundUser
        {
            Subject = Sub, Email = Email, EmailVerified = EmailVerified, Name = Name, GivenName = GivenName,
            FamilyName = FamilyName, AuthMethod = AuthMethod, BusinessId = BusinessId, BusinessSlug = BusinessSlug,
            BusinessRole = BusinessRole, Categories = QrboundClaims.Categories(claims),
            Organization = QrboundClaims.Organization(claims), ExpiresAt = ExpiresAt, Claims = claims
        };
    }
}
