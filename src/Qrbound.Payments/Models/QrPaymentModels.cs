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

/// <summary>How a QR payment was paid.</summary>
public static class QrPaymentMethods
{
    /// <summary>A card typed into CardPointe's card fields on the pay page.</summary>
    public const string Card = "Card";

    /// <summary>A saved card, approved with the customer's fingerprint or face (passkey).</summary>
    public const string SavedCard = "SavedCard";

    /// <summary>A card entered on Fiserv's CardPointe Hosted Payment Page.</summary>
    public const string HostedPage = "HostedPage";
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

    /// <summary>Whether the business lets signed-in customers pay with a saved card and their fingerprint.</summary>
    public bool SavedCardsAccepted { get; set; } = true;

    /// <summary>Whether the customer can add a tip.</summary>
    public bool TipsEnabled { get; set; }

    /// <summary>Suggested tips, in percent of the bill (e.g. 15, 18, 20).</summary>
    public List<int> TipPercentages { get; set; } = new();

    /// <summary>Whether the customer can type their own tip amount.</summary>
    public bool AllowCustomTip { get; set; }

    /// <summary>The largest tip accepted, in percent of the bill.</summary>
    public int MaxTipPercent { get; set; }

    /// <summary>Whether a card can be paid on Fiserv's CardPointe Hosted Payment Page.</summary>
    public bool HostedPageAvailable { get; set; }

    /// <summary>True once the customer has been sent to the hosted payment page for this bill.</summary>
    public bool HostedPageStarted { get; set; }

    /// <summary>Once paid: the tip, in cents.</summary>
    public long TipCents { get; set; }

    /// <summary>Once paid: what was charged (bill plus tip), in cents.</summary>
    public long? PaidAmountCents { get; set; }

    /// <summary>Once paid: one of <see cref="QrPaymentMethods"/>.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>The tip for a percentage of the bill, rounded to the cent; the server uses the same rule.</summary>
    public static long TipFor(long amountCents, int percent) =>
        (long)Math.Round(amountCents * percent / 100m, MidpointRounding.AwayFromZero);
}

/// <summary>The customer's tip choice; the server answers with a locked quote.</summary>
public sealed class QrPaymentQuoteRequest
{
    /// <summary>Tip in cents; 0 for none.</summary>
    [Range(0, long.MaxValue)]
    public long TipCents { get; set; }
}

/// <summary>
/// A locked total for one bill: the bill, the tip and what will be charged. Every way of paying
/// names the quote, and the server charges exactly its total.
/// </summary>
public sealed class QrPaymentQuote
{
    public Guid QuoteId { get; set; }
    public Guid PaymentId { get; set; }
    public long AmountCents { get; set; }
    public long TipCents { get; set; }
    public long TotalCents { get; set; }
    public string Currency { get; set; } = "USD";
    public DateTime ExpiresAt { get; set; }
}

/// <summary>Send the customer to Fiserv's hosted payment page for a quote.</summary>
public sealed class HostedPageStartRequest
{
    public Guid QuoteId { get; set; }
}

/// <summary>Where to send the customer to pay on Fiserv's hosted payment page.</summary>
public sealed class HostedPageStart
{
    /// <summary>The hosted payment page, with the total and the bill's invoice number filled in.</summary>
    public string Url { get; set; } = string.Empty;

    public long TotalCents { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
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

    /// <summary>The quote to pay (bill plus tip). Null pays the bill with no tip.</summary>
    public Guid? QuoteId { get; set; }
}

/// <summary>Staff create a QR payment request with this.</summary>
public sealed class CreateQrPaymentRequest
{
    /// <summary>Amount in cents.</summary>
    [Range(1, long.MaxValue)]
    public long AmountCents { get; set; }

    [StringLength(250)]
    public string? Description { get; set; }

    /// <summary>How long the QR code can be paid, in minutes (5 to 1440). Null uses the business's default.</summary>
    [Range(5, 1440)]
    public int? ExpiresInMinutes { get; set; }
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

    /// <summary>The tip, in cents.</summary>
    public long TipCents { get; set; }

    /// <summary>What was charged (bill plus tip), in cents, once paid.</summary>
    public long? PaidAmountCents { get; set; }

    /// <summary>One of <see cref="QrPaymentMethods"/>, once paid.</summary>
    public string? PaymentMethod { get; set; }

    /// <summary>When the customer was sent to Fiserv's hosted payment page, if they were.</summary>
    public DateTime? HostedStartedAt { get; set; }

    /// <summary>
    /// A second approved charge for this bill (CardPointe retref), e.g. paid on the hosted page after
    /// it was already paid. Refund it in CardPointe.
    /// </summary>
    public string? ExtraTransactionId { get; set; }
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
