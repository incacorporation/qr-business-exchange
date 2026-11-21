using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// Status values for a QR login session.
/// </summary>
public static class QrLoginStatus
{
    /// <summary>QR displayed, waiting to be scanned.</summary>
    public const string Pending = "Pending";
    /// <summary>A signed-in phone opened the QR link.</summary>
    public const string Scanned = "Scanned";
    /// <summary>The phone user approved with a passkey.</summary>
    public const string Approved = "Approved";
    /// <summary>The phone user rejected the request.</summary>
    public const string Denied = "Denied";
    /// <summary>The desktop collected its tokens; the session is finished.</summary>
    public const string Consumed = "Consumed";
    /// <summary>The session timed out.</summary>
    public const string Expired = "Expired";
}

/// <summary>
/// A "scan with your signed-in phone" login request. The desktop browser displays
/// a QR code; a phone already signed in to the core scans it, the user confirms with
/// a passkey, and the desktop (which holds the poll secret) receives a session.
/// </summary>
public class QrLoginSession
{
    [Key]
    public Guid QrLoginSessionId { get; set; } = Guid.NewGuid();

    /// <summary>
    /// SHA-256 hash of the secret only the requesting (desktop) browser knows.
    /// </summary>
    [Required]
    [StringLength(128)]
    public string PollSecretHash { get; set; } = string.Empty;

    /// <summary>
    /// The application the desktop is trying to sign in to (null = the core portal).
    /// </summary>
    public Guid? SsoApplicationId { get; set; }

    [Required]
    [StringLength(20)]
    public string Status { get; set; } = QrLoginStatus.Pending;

    /// <summary>
    /// Short code shown on both screens so the user can confirm they match.
    /// </summary>
    [Required]
    [StringLength(10)]
    public string VerificationCode { get; set; } = string.Empty;

    [StringLength(64)]
    public string? RequesterIp { get; set; }

    [StringLength(512)]
    public string? RequesterUserAgent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? ScannedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Guid? ApprovedUserId { get; set; }

    public virtual SsoApplication? Application { get; set; }
    public virtual User? ApprovedUser { get; set; }
}
