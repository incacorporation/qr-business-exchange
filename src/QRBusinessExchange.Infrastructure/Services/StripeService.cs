using Microsoft.Extensions.Options;
using QRBusinessExchange.Infrastructure.Configuration;
using Stripe;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Service for Stripe payment processing using Stripe .NET SDK
/// </summary>
public class StripeService
{
    private readonly PaymentSettings _paymentSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeService"/> class
    /// </summary>
    /// <param name="paymentSettings">Payment configuration settings</param>
    public StripeService(IOptions<PaymentSettings> paymentSettings)
    {
        _paymentSettings = paymentSettings?.Value ?? throw new ArgumentNullException(nameof(paymentSettings));
        ValidateSettings();

        StripeConfiguration.ApiKey = _paymentSettings.StripeApiKey;
    }

    /// <summary>
    /// Creates a Stripe payment intent
    /// </summary>
    /// <param name="amount">The payment amount in cents (e.g., 1000 for $10.00)</param>
    /// <param name="currency">The currency code (e.g., usd)</param>
    /// <param name="userId">The user ID for metadata</param>
    /// <param name="subscriptionType">The subscription type for metadata</param>
    /// <returns>The Stripe client secret for payment confirmation</returns>
    /// <exception cref="InvalidOperationException">Thrown when payment intent creation fails</exception>
    public async Task<string> CreatePaymentIntentAsync(
        long amount, 
        string currency, 
        Guid userId, 
        string subscriptionType)
    {
        if (amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(currency))
        {
            throw new ArgumentException("Currency cannot be null or empty", nameof(currency));
        }

        try
        {
            var service = new PaymentIntentService();
            var options = new PaymentIntentCreateOptions
            {
                Amount = amount,
                Currency = currency.ToLower(),
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true,
                },
                Metadata = new Dictionary<string, string>
                {
                    { "user_id", userId.ToString() },
                    { "subscription_type", subscriptionType },
                    { "platform", "QR Business Exchange" }
                },
                Description = $"QR Business Exchange - {subscriptionType} Subscription"
            };

            var paymentIntent = await service.CreateAsync(options);

            if (paymentIntent == null || string.IsNullOrEmpty(paymentIntent.ClientSecret))
            {
                throw new InvalidOperationException("Stripe payment intent creation failed - no client secret returned");
            }

            return paymentIntent.ClientSecret;
        }
        catch (StripeException ex)
        {
            throw new InvalidOperationException($"Stripe API error: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Error creating Stripe payment intent", ex);
        }
    }

    /// <summary>
    /// Confirms a Stripe payment intent
    /// </summary>
    /// <param name="paymentIntentId">The Stripe payment intent ID</param>
    /// <returns>True if confirmation was successful, false otherwise</returns>
    /// <exception cref="InvalidOperationException">Thrown when payment confirmation fails</exception>
    public async Task<bool> ConfirmPaymentIntentAsync(string paymentIntentId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            throw new ArgumentException("Payment intent ID cannot be null or empty", nameof(paymentIntentId));
        }

        try
        {
            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId);

            if (paymentIntent == null)
            {
                return false;
            }

            // Check if payment intent is in a successful state
            return paymentIntent.Status == "succeeded" || paymentIntent.Status == "processing";
        }
        catch (StripeException ex)
        {
            throw new InvalidOperationException($"Stripe API error: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Error confirming Stripe payment {paymentIntentId}", ex);
        }
    }

    /// <summary>
    /// Retrieves a Stripe payment intent
    /// </summary>
    /// <param name="paymentIntentId">The Stripe payment intent ID</param>
    /// <returns>The payment intent</returns>
    /// <exception cref="InvalidOperationException">Thrown when retrieval fails</exception>
    public async Task<PaymentIntent> GetPaymentIntentAsync(string paymentIntentId)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            throw new ArgumentException("Payment intent ID cannot be null or empty", nameof(paymentIntentId));
        }

        try
        {
            var service = new PaymentIntentService();
            var paymentIntent = await service.GetAsync(paymentIntentId);

            if (paymentIntent == null)
            {
                throw new InvalidOperationException($"Payment intent {paymentIntentId} not found");
            }

            return paymentIntent;
        }
        catch (StripeException ex)
        {
            throw new InvalidOperationException($"Stripe API error: {ex.Message}", ex);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Error retrieving Stripe payment {paymentIntentId}", ex);
        }
    }

    /// <summary>
    /// Validates Stripe settings configuration
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when settings are invalid</exception>
    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_paymentSettings.StripeApiKey))
        {
            throw new InvalidOperationException("Stripe API Key is not configured");
        }

        if (string.IsNullOrWhiteSpace(_paymentSettings.StripePublishableKey))
        {
            throw new InvalidOperationException("Stripe Publishable Key is not configured");
        }
    }
}
