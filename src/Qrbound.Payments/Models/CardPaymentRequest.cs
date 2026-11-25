using System.ComponentModel.DataAnnotations;

namespace Qrbound.Payments.Models;

/// <summary>
/// A card charge sent from the browser to the server.
/// Carries the CardPointe token for the card, never the card number or CVV.
/// </summary>
public sealed class CardPaymentRequest
{
    /// <summary>Token the hosted tokenizer returned for the card.</summary>
    [Required, StringLength(64, MinimumLength = 4)]
    public string PaymentToken { get; set; } = string.Empty;

    /// <summary>Card expiry as MMYY, returned by the tokenizer with useexpiry=true.</summary>
    [Required, RegularExpression(@"^(0[1-9]|1[0-2])\d{2}$", ErrorMessage = "Expiry must be MMYY.")]
    public string Expiry { get; set; } = string.Empty;

    /// <summary>Name on the card.</summary>
    [Required, StringLength(100)]
    public string CardholderName { get; set; } = string.Empty;

    /// <summary>Amount in cents: 1000 is $10.00.</summary>
    [Range(1, long.MaxValue)]
    public long AmountCents { get; set; }

    /// <summary>ISO 4217 currency code; must match the merchant's currency.</summary>
    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "USD";

    /// <summary>Your invoice or order number, sent to CardPointe as orderid.</summary>
    [Required, StringLength(19)]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>What the charge is for (kept with your records, not sent to CardPointe).</summary>
    [StringLength(250)]
    public string? Description { get; set; }

    /// <summary>Email for the receipt.</summary>
    [EmailAddress, StringLength(254)]
    public string? Email { get; set; }
}
