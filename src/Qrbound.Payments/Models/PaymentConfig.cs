namespace Qrbound.Payments.Models;

/// <summary>
/// Public payment settings the server sends to the browser to set up the card form.
/// Holds no secrets: the gateway username and password never leave the server.
/// </summary>
public sealed class PaymentConfig
{
    /// <summary>CardPointe merchant ID. Public: the hosted tokenizer needs it.</summary>
    public string? MerchantId { get; set; }

    /// <summary>
    /// URL of CardPointe's hosted iFrame tokenizer, for example
    /// https://fts-uat.cardconnect.com/itoke/ajax-tokenizer.html in test.
    /// </summary>
    public string? TokenizerUrl { get; set; }

    /// <summary>"test" or "production".</summary>
    public string Environment { get; set; } = "test";

    /// <summary>True outside production: test cards are accepted.</summary>
    public bool IsTestMode { get; set; } = true;

    /// <summary>The one currency the merchant account settles in (ISO 4217).</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Smallest charge allowed, in cents.</summary>
    public long MinAmountCents { get; set; }

    /// <summary>Largest charge allowed, in cents.</summary>
    public long MaxAmountCents { get; set; }
}
