using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using QRBusinessExchange.Infrastructure.Configuration;
using RestSharp;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Service for PayPal payment processing using PayPal REST API v2
/// </summary>
public class PayPalService
{
    private readonly PaymentSettings _paymentSettings;
    private readonly RestClient _client;
    private const string SandboxBaseUrl = "https://api-m.sandbox.paypal.com";
    private const string ProductionBaseUrl = "https://api-m.paypal.com";

    /// <summary>
    /// Initializes a new instance of the <see cref="PayPalService"/> class
    /// </summary>
    /// <param name="paymentSettings">Payment configuration settings</param>
    public PayPalService(IOptions<PaymentSettings> paymentSettings)
    {
        _paymentSettings = paymentSettings?.Value ?? throw new ArgumentNullException(nameof(paymentSettings));
        ValidateSettings();

        // Use sandbox for development (if ClientId contains "sandbox" or starts with "AX")
        var baseUrl = _paymentSettings.PayPalClientId.Contains("sandbox", StringComparison.OrdinalIgnoreCase) 
            ? SandboxBaseUrl 
            : ProductionBaseUrl;
        
        _client = new RestClient(baseUrl);
    }

    /// <summary>
    /// Creates a PayPal payment order
    /// </summary>
    /// <param name="amount">The payment amount</param>
    /// <param name="currency">The currency code (e.g., USD)</param>
    /// <param name="returnUrl">The URL to redirect after approval</param>
    /// <param name="cancelUrl">The URL to redirect if cancelled</param>
    /// <param name="description">Payment description</param>
    /// <returns>The PayPal order ID and approval URL</returns>
    /// <exception cref="InvalidOperationException">Thrown when payment creation fails</exception>
    public async Task<(string OrderId, string ApprovalUrl)> CreatePaymentAsync(
        decimal amount, 
        string currency, 
        string returnUrl, 
        string cancelUrl,
        string description = "QR Business Exchange Subscription")
    {
        try
        {
            var accessToken = await GetAccessTokenAsync();

            var request = new RestRequest("/v2/checkout/orders", Method.Post);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddHeader("Content-Type", "application/json");

            var orderRequest = new
            {
                intent = "CAPTURE",
                purchase_units = new[]
                {
                    new
                    {
                        amount = new
                        {
                            currency_code = currency,
                            value = amount.ToString("F2")
                        },
                        description
                    }
                },
                application_context = new
                {
                    return_url = returnUrl,
                    cancel_url = cancelUrl,
                    brand_name = "QR Business Exchange",
                    user_action = "PAY_NOW"
                }
            };

            request.AddJsonBody(orderRequest);

            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
            {
                throw new InvalidOperationException(
                    $"PayPal order creation failed: {response.ErrorMessage ?? response.StatusCode.ToString()}");
            }

            var result = JsonConvert.DeserializeObject<dynamic>(response.Content);
            
            if (result == null)
            {
                throw new InvalidOperationException("PayPal response was null or invalid");
            }

            string orderId = result.id;
            string? approvalUrl = null;

            // Extract approval URL from links
            foreach (var link in result.links)
            {
                if (link.rel == "approve")
                {
                    approvalUrl = link.href;
                    break;
                }
            }

            if (string.IsNullOrEmpty(approvalUrl))
            {
                throw new InvalidOperationException("PayPal approval URL not found in response");
            }

            return (orderId, approvalUrl);
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Error creating PayPal payment", ex);
        }
    }

    /// <summary>
    /// Captures a PayPal payment after user approval
    /// </summary>
    /// <param name="orderId">The PayPal order ID</param>
    /// <returns>True if capture was successful, false otherwise</returns>
    /// <exception cref="InvalidOperationException">Thrown when payment capture fails</exception>
    public async Task<bool> CapturePaymentAsync(string orderId)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            throw new ArgumentException("Order ID cannot be null or empty", nameof(orderId));
        }

        try
        {
            var accessToken = await GetAccessTokenAsync();

            var request = new RestRequest($"/v2/checkout/orders/{orderId}/capture", Method.Post);
            request.AddHeader("Authorization", $"Bearer {accessToken}");
            request.AddHeader("Content-Type", "application/json");

            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful)
            {
                throw new InvalidOperationException(
                    $"PayPal payment capture failed: {response.ErrorMessage ?? response.StatusCode.ToString()}");
            }

            if (string.IsNullOrEmpty(response.Content))
            {
                return false;
            }

            var result = JsonConvert.DeserializeObject<dynamic>(response.Content);
            
            if (result == null)
            {
                return false;
            }

            string status = result.status;
            return status == "COMPLETED";
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException($"Error capturing PayPal payment {orderId}", ex);
        }
    }

    /// <summary>
    /// Generates an OAuth access token for PayPal API authentication
    /// </summary>
    /// <returns>The access token</returns>
    /// <exception cref="InvalidOperationException">Thrown when token generation fails</exception>
    private async Task<string> GetAccessTokenAsync()
    {
        try
        {
            var request = new RestRequest("/v1/oauth2/token", Method.Post);
            request.AddHeader("Accept", "application/json");
            request.AddHeader("Accept-Language", "en_US");
            
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.ASCII.GetBytes(
                    $"{_paymentSettings.PayPalClientId}:{_paymentSettings.PayPalClientSecret}"));
            
            request.AddHeader("Authorization", $"Basic {credentials}");
            request.AddParameter("grant_type", "client_credentials", ParameterType.GetOrPost);

            var response = await _client.ExecuteAsync(request);

            if (!response.IsSuccessful || string.IsNullOrEmpty(response.Content))
            {
                throw new InvalidOperationException(
                    $"PayPal OAuth token request failed: {response.ErrorMessage ?? response.StatusCode.ToString()}");
            }

            var result = JsonConvert.DeserializeObject<dynamic>(response.Content);
            
            if (result?.access_token == null)
            {
                throw new InvalidOperationException("PayPal access token not found in response");
            }

            string accessToken = result.access_token;
            return accessToken;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException("Error getting PayPal access token", ex);
        }
    }

    /// <summary>
    /// Validates PayPal settings configuration
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when settings are invalid</exception>
    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_paymentSettings.PayPalClientId))
        {
            throw new InvalidOperationException("PayPal Client ID is not configured");
        }

        if (string.IsNullOrWhiteSpace(_paymentSettings.PayPalClientSecret))
        {
            throw new InvalidOperationException("PayPal Client Secret is not configured");
        }
    }
}
