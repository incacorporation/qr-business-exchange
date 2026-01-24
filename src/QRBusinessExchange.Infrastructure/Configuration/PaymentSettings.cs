namespace QRBusinessExchange.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for payment gateway integrations
/// </summary>
public class PaymentSettings
{
    /// <summary>
    /// Gets or sets the PayPal client ID for OAuth authentication
    /// </summary>
    /// <remarks>
    /// Obtained from the PayPal Developer Dashboard.
    /// Different values for sandbox and production environments.
    /// </remarks>
    public string PayPalClientId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the PayPal client secret for OAuth authentication
    /// </summary>
    /// <remarks>
    /// Obtained from the PayPal Developer Dashboard.
    /// Should be stored securely and never exposed to clients.
    /// </remarks>
    public string PayPalClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Stripe API secret key
    /// </summary>
    /// <remarks>
    /// Obtained from the Stripe Dashboard.
    /// Use test keys for development (starts with sk_test_) and live keys for production (starts with sk_live_).
    /// Should be stored securely and never exposed to clients.
    /// </remarks>
    public string StripeApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Stripe publishable key
    /// </summary>
    /// <remarks>
    /// Obtained from the Stripe Dashboard.
    /// This key can be safely exposed to clients and is used for client-side integrations.
    /// Use test keys for development (starts with pk_test_) and live keys for production (starts with pk_live_).
    /// </remarks>
    public string StripePublishableKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Clover API key
    /// </summary>
    /// <remarks>
    /// Obtained from the Clover Developer Dashboard.
    /// Different keys for sandbox and production environments.
    /// Should be stored securely and never exposed to clients.
    /// </remarks>
    public string CloverApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Clover merchant ID
    /// </summary>
    /// <remarks>
    /// Unique identifier for the merchant account in Clover.
    /// Obtained from the Clover Dashboard.
    /// </remarks>
    public string CloverMerchantId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the maximum allowed distance in yards for payment location verification
    /// </summary>
    /// <remarks>
    /// Default is 50 yards. This enforces the distance verification rule between
    /// payment initiation and verification locations.
    /// </remarks>
    public decimal MaxVerificationDistanceYards { get; set; } = 50;

    /// <summary>
    /// Gets or sets the brand name to display in payment gateways
    /// </summary>
    /// <remarks>
    /// This name will be shown to users during payment processes across all gateways.
    /// </remarks>
    public string BrandName { get; set; } = "QR Business Exchange";
}
