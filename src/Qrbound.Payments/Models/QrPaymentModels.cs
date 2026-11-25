using System.ComponentModel.DataAnnotations;

namespace Qrbound.Payments.Models;

/// <summary>States of a QR payment request, as the API reports them.</summary>
public static class QrPaymentStates
{
    public const string Open = "Open";
    public const string Processing = "Processing";
    public const string Paid = "Paid";
    public const string Cancelled = "Cancelled";
    public const string Locked = "Locked";
    public const string NeedsReview = "NeedsReview";

    /// <summary>Reported for an Open request past its ExpiresAt.</summary>
    public const string Expired = "Expired";

    /// <summary>True when no more payment will be taken for the request.</summary>
    public static bool IsFinal(string? state) => state is Paid or Cancelled or Locked or Expired;
}

/// <summary>What anyone with the QR link sees: who is asking, how much, and whether it's paid.</summary>
public sealed class QrPaymentInfo
{
    public Guid Id { get; set; }
    public string BusinessName { get; set; } = string.Empty;
    public string? BusinessLogoUrl { get; set; }
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Description { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>One of <see cref="QrPaymentStates"/>.</summary>
    public string Status { get; set; } = QrPaymentStates.Open;

    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>CardPointe reference, once paid.</summary>
    public string? TransactionId { get; set; }
}

/// <summary>A payer's card for a QR payment request. The amount comes from the request, not from here.</summary>
public sealed class QrPaymentPayRequest
{
    [Required, StringLength(64, MinimumLength = 4)]
    public string PaymentToken { get; set; } = string.Empty;

    /// <summary>MMYY.</summary>
    [Required, RegularExpression(@"^(0[1-9]|1[0-2])\d{2}$", ErrorMessage = "Expiry must be MMYY.")]
    public string Expiry { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string CardholderName { get; set; } = string.Empty;

    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }
}

/// <summary>Staff create a QR payment request with this.</summary>
public sealed class CreateQrPaymentRequest
{
    /// <summary>Amount in cents.</summary>
    [Range(1, long.MaxValue)]
    public long AmountCents { get; set; }

    [StringLength(250)]
    public string? Description { get; set; }

    /// <summary>How long the QR code can be paid, in minutes (5 to 1440). Default 30.</summary>
    [Range(5, 1440)]
    public int ExpiresInMinutes { get; set; } = 30;
}

/// <summary>A QR payment request as staff of the business see it.</summary>
public sealed class QrPaymentDetails
{
    public Guid Id { get; set; }
    public Guid BusinessId { get; set; }
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "USD";
    public string? Description { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>One of <see cref="QrPaymentStates"/>.</summary>
    public string Status { get; set; } = QrPaymentStates.Open;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? TransactionId { get; set; }
    public string? AuthorizationCode { get; set; }
    public string? PayerName { get; set; }
    public string? PayerEmail { get; set; }
    public int FailedAttempts { get; set; }
    public string? LastError { get; set; }
}

/// <summary>Staff's decision on a request in NeedsReview, after checking it in CardPointe.</summary>
public sealed class ResolveQrPaymentRequest
{
    /// <summary>True: CardPointe shows the charge went through. False: it didn't; take payment again.</summary>
    public bool Paid { get; set; }

    /// <summary>CardPointe retref of the charge, when Paid.</summary>
    [StringLength(30)]
    public string? TransactionId { get; set; }
}
