using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// A request for a card payment to a business, shown as a QR code. Whoever scans it pays the
/// amount stored here (never an amount the payer sends) through CardPointe, once.
/// </summary>
public class QrPaymentRequest
{
    [Key]
    public Guid QrPaymentRequestId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid BusinessId { get; set; }

    /// <summary>The staff member who created the request.</summary>
    [Required]
    public Guid CreatedByUserId { get; set; }

    /// <summary>Amount in cents.</summary>
    public long AmountCents { get; set; }

    [Required]
    [StringLength(3)]
    public string Currency { get; set; } = "USD";

    [StringLength(250)]
    public string? Description { get; set; }

    /// <summary>Sent to CardPointe as orderid (at most 19 characters); unique.</summary>
    [Required]
    [StringLength(19)]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>See <see cref="QrPaymentStatus"/>.</summary>
    [Required]
    [StringLength(20)]
    public string Status { get; set; } = QrPaymentStatus.Open;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime ExpiresAt { get; set; }

    public DateTime? PaidAt { get; set; }

    /// <summary>CardPointe retref of the approved charge.</summary>
    [StringLength(30)]
    public string? TransactionId { get; set; }

    [StringLength(20)]
    public string? AuthorizationCode { get; set; }

    [StringLength(100)]
    public string? PayerName { get; set; }

    [StringLength(254)]
    public string? PayerEmail { get; set; }

    /// <summary>Declined or failed card attempts; the request locks at <see cref="QrPaymentStatus.MaxFailedAttempts"/>.</summary>
    public int FailedAttempts { get; set; }

    /// <summary>Last gateway message for a failed attempt, for staff.</summary>
    [StringLength(250)]
    public string? LastError { get; set; }

    /// <summary>Changes on every update, so two payers can't both claim the request.</summary>
    [ConcurrencyCheck]
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public virtual Business Business { get; set; } = null!;
}

/// <summary>States of a <see cref="QrPaymentRequest"/>.</summary>
public static class QrPaymentStatus
{
    /// <summary>Waiting to be paid.</summary>
    public const string Open = "Open";

    /// <summary>A charge is being sent to CardPointe right now.</summary>
    public const string Processing = "Processing";

    public const string Paid = "Paid";

    /// <summary>Cancelled by staff.</summary>
    public const string Cancelled = "Cancelled";

    /// <summary>Too many declined cards; no more attempts are taken.</summary>
    public const string Locked = "Locked";

    /// <summary>CardPointe didn't answer in time: the charge may or may not have gone through. Staff must check.</summary>
    public const string NeedsReview = "NeedsReview";

    public const int MaxFailedAttempts = 5;
}
