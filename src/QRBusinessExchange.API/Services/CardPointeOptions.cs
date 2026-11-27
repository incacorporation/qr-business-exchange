namespace QRBusinessExchange.API.Services;

/// <summary>
/// CardPointe (Fiserv CardConnect) settings, read from the "CardPointe" configuration section.
/// ApiUsername and ApiPassword come only from user-secrets (development) or the
/// CardPointe__ApiUsername / CardPointe__ApiPassword environment variables (servers); never appsettings.
/// </summary>
public sealed class CardPointeOptions
{
    public const string SectionName = "CardPointe";

    /// <summary>Merchant ID (merchid). Public.</summary>
    public string MerchantId { get; set; } = string.Empty;

    /// <summary>Gateway API username. Secret.</summary>
    public string ApiUsername { get; set; } = string.Empty;

    /// <summary>Gateway API password. Secret.</summary>
    public string ApiPassword { get; set; } = string.Empty;

    /// <summary>
    /// REST base URL without a trailing endpoint, e.g. https://fts-uat.cardconnect.com/cardconnect/rest
    /// (test) or https://&lt;site&gt;.cardconnect.com/cardconnect/rest (production). "/auth" is added here.
    /// </summary>
    public string ApiUrl { get; set; } = string.Empty;

    /// <summary>Hosted iFrame tokenizer URL, e.g. https://fts-uat.cardconnect.com/itoke/ajax-tokenizer.html.</summary>
    public string TokenizerUrl { get; set; } = string.Empty;

    /// <summary>"test" or "production".</summary>
    public string Environment { get; set; } = "test";

    /// <summary>The currency the merchant account settles in.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Smallest charge allowed, in cents. Default $1.00.</summary>
    public long MinAmountCents { get; set; } = 100;

    /// <summary>Largest charge allowed, in cents. Default $10,000.00.</summary>
    public long MaxAmountCents { get; set; } = 1_000_000;

    /// <summary>How long to wait for CardPointe before giving up.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Approve every charge without calling CardPointe. Honoured only outside the Production
    /// host environment; ignored (with an error logged) in Production.
    /// </summary>
    public bool UseMockApi { get; set; }

    /// <summary>
    /// The merchant's CardPointe Hosted Payment Page, e.g. https://&lt;site&gt;.securepayments.cardpointe.com/pay.
    /// Customers are sent there with ?total=…&amp;invoice=… filled in. Empty: cards are paid in CardPointe's
    /// card fields on qrbound's pay page instead.
    /// </summary>
    public string HostedPageUrl { get; set; } = string.Empty;

    /// <summary>
    /// Secret the hosted page's webhook must send as ?key=… (set the webhook URL in the HPP Admin's
    /// Connect tab to https://&lt;api&gt;/api/pay/hosted/webhook?key=&lt;this&gt;). Secret: user-secrets or
    /// CardPointe__HostedPageWebhookKey only. A webhook is never trusted on its own: each one makes
    /// qrbound look the order up with CardPointe.
    /// </summary>
    public string HostedPageWebhookKey { get; set; } = string.Empty;

    public bool IsProduction => string.Equals(Environment, "production", StringComparison.OrdinalIgnoreCase);

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(MerchantId) &&
        !string.IsNullOrWhiteSpace(ApiUsername) &&
        !string.IsNullOrWhiteSpace(ApiPassword) &&
        !string.IsNullOrWhiteSpace(ApiUrl);
}
