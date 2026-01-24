using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using QRBusinessExchange.Infrastructure.Configuration;
using RestSharp;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Service for Clover payment processing using Clover REST API
/// </summary>
public class CloverService
{
    private readonly PaymentSettings _paymentSettings;
    private readonly RestClient _client;
    private const string SandboxBaseUrl = "https://sandbox.dev.clover.com";
    private const string ProductionBaseUrl = "https://api.clover.com";

    /// <summary>
    /// Initializes a new instance of the <see cref="CloverService"/> class
    /// </summary>
    /// <param name="paymentSettings">Payment configuration settings</param>
    public CloverService(IOptions<PaymentSettings> paymentSettings)
    {
        _paymentSettings = paymentSettings?.Value ?? throw new ArgumentNullException(nameof(paymentSettings));
        ValidateSettings();

        // Use sandbox if API key contains "sandbox" or starts with test prefix
        var baseUrl = _paymentSettings.CloverApiKey.Contains("sandbox", StringComparison.OrdinalIgnoreCase)
            ? SandboxBaseUrl
            : ProductionBaseUrl;

        _client = new RestClient(baseUrl);
    }

    /// <summary>
    /// Creates a Clover charge
    /// </summary>
    /// <param name="amount">The payment amount in cents (e.g., 1000 for $10.00)</param>
    /// <param name="currency">The currency code (e.g., USD)</param>
    /// <param name="source">The payment source (token or card)</param>
    /// <param name="userId">The user ID for metadata</param>
    /// <param name="subscriptionType">The subscription type for metadata</param>
    /// <returns>The Clover payment ID and status</returns>
    /// <exception cref="InvalidOperationException">Thrown when charge creation fails</exception>
    public async Task<(string PaymentId, string Status)> CreateChargeAsync(
        long amount,
        string currency,
        string source,
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

        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException("Payment source cannot be null or empty", nameof(source));
        }

        try
        {
            var request = new RestRequest($"/v1/charges", Method.Post);
            request.AddHeader("Authorization", $"Bearer {_paymentSettings.CloverApiKey}");
            request.AddHeader("Content-Type", "application/json");

            var chargeRequest = new
            {
                amount,
                currency = currency.ToUpper(),
                source,
                description = $"QR Business Exchange - {subscriptionType} Subscription",
                metadata = new
                {
                    user_id = userId.ToString(),
                    subscription_type = subscriptionType,
                    platform = "QR Business Exchange",
                    merchant_id = _paymentSettings.CloverMerchantId
                }
            };

            request.AddJsonBody(chargeRequest);

            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
            {
                throw new InvalidOperationException(
                    $"Clover charge creation failed: {response.ErrorMessage ?? response.StatusCode.ToString()}");
            }

            var result = JsonConvert.DeserializeObject<dynamic>(response.Content);

            if (result == null)
            {
                throw new InvalidOperationException("Clover response was null or invalid");
            }

            string paymentId = result.id;
            string status = result.status ?? "unknown";

            return (paymentId, status);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Error creating Clover charge", ex);
        }
    }

    /// <summary>
    /// Retrieves a Clover charge
    /// </summary>
    /// <param name="chargeId">The Clover charge ID</param>
    /// <returns>The charge details including status</returns>
    /// <exception cref="InvalidOperationException">Thrown when retrieval fails</exception>
    public async Task<(string ChargeId, string Status, long Amount)> GetChargeAsync(string chargeId)
    {
        if (string.IsNullOrWhiteSpace(chargeId))
        {
            throw new ArgumentException("Charge ID cannot be null or empty", nameof(chargeId));
        }

        try
        {
            var request = new RestRequest($"/v1/charges/{chargeId}", Method.Get);
            request.AddHeader("Authorization", $"Bearer {_paymentSettings.CloverApiKey}");

            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
            {
                throw new InvalidOperationException(
                    $"Clover charge retrieval failed: {response.ErrorMessage ?? response.StatusCode.ToString()}");
            }

            var result = JsonConvert.DeserializeObject<dynamic>(response.Content);

            if (result == null)
            {
                throw new InvalidOperationException("Clover response was null or invalid");
            }

            string id = result.id;
            string status = result.status ?? "unknown";
            long amount = result.amount ?? 0;

            return (id, status, amount);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Error retrieving Clover charge {chargeId}", ex);
        }
    }

    /// <summary>
    /// Verifies if a Clover charge was successful
    /// </summary>
    /// <param name="chargeId">The Clover charge ID</param>
    /// <returns>True if charge was successful, false otherwise</returns>
    /// <exception cref="InvalidOperationException">Thrown when verification fails</exception>
    public async Task<bool> VerifyChargeAsync(string chargeId)
    {
        if (string.IsNullOrWhiteSpace(chargeId))
        {
            throw new ArgumentException("Charge ID cannot be null or empty", nameof(chargeId));
        }

        try
        {
            var (_, status, _) = await GetChargeAsync(chargeId);
            
            // Clover charge statuses: succeeded, pending, failed
            return status.Equals("succeeded", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error verifying Clover charge {chargeId}", ex);
        }
    }

    /// <summary>
    /// Validates Clover settings configuration
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when settings are invalid</exception>
    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_paymentSettings.CloverApiKey))
        {
            throw new InvalidOperationException("Clover API Key is not configured");
        }

        if (string.IsNullOrWhiteSpace(_paymentSettings.CloverMerchantId))
        {
            throw new InvalidOperationException("Clover Merchant ID is not configured");
        }
    }
}
