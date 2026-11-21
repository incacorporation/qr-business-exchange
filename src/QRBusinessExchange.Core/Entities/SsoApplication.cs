using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// An application owned by a business that signs users in through the SSO core.
/// Each application receives JWTs whose audience is its <see cref="ClientId"/>.
/// </summary>
public class SsoApplication
{
    [Key]
    public Guid SsoApplicationId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid BusinessId { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Public identifier used by the application ("client_id").
    /// </summary>
    [Required]
    [StringLength(100)]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// SHA-256 hash of the client secret. Null for public clients (SPA / mobile),
    /// which must use PKCE instead.
    /// </summary>
    [StringLength(128)]
    public string? ClientSecretHash { get; set; }

    /// <summary>
    /// Newline-separated list of exact redirect URIs the application may use.
    /// </summary>
    [Required]
    public string RedirectUris { get; set; } = string.Empty;

    /// <summary>
    /// When true only members of the owning business may sign in to this application.
    /// </summary>
    public bool RequireMembership { get; set; } = false;

    /// <summary>
    /// Lifetime of issued access/ID tokens in minutes.
    /// </summary>
    public int TokenLifetimeMinutes { get; set; } = 60;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public virtual Business Business { get; set; } = null!;

    public bool IsConfidential => !string.IsNullOrEmpty(ClientSecretHash);

    public IEnumerable<string> GetRedirectUris() =>
        RedirectUris.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

/// <summary>
/// Single-use authorization code handed to an application after the user signs in.
/// The application exchanges it server-to-server for a signed JWT.
/// </summary>
public class SsoAuthorizationCode
{
    [Key]
    public Guid SsoAuthorizationCodeId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// SHA-256 hash of the code (the raw code is never stored).
    /// </summary>
    [Required]
    [StringLength(128)]
    public string CodeHash { get; set; } = string.Empty;

    [Required]
    public Guid SsoApplicationId { get; set; }

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(1000)]
    public string RedirectUri { get; set; } = string.Empty;

    [StringLength(128)]
    public string? CodeChallenge { get; set; }

    [StringLength(10)]
    public string? CodeChallengeMethod { get; set; }

    [StringLength(256)]
    public string? Nonce { get; set; }

    /// <summary>
    /// How the user authenticated: "passkey" or "qr".
    /// </summary>
    [Required]
    [StringLength(20)]
    public string AuthMethod { get; set; } = "passkey";

    public DateTime AuthTime { get; set; } = DateTime.UtcNow;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ConsumedAt { get; set; }

    public virtual SsoApplication Application { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
