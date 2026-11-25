using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Qrbound.Payments.Models;

namespace QRBusinessExchange.API.Services;

/// <summary>
/// CardPointe (Fiserv CardConnect) gateway client. Sends tokenized sales to the /auth endpoint
/// with capture=Y. Logs never contain the credentials, the Authorization header or the full token.
/// </summary>
public sealed class CardPointeService : ICardPointeService
{
    private readonly HttpClient _httpClient;
    private readonly CardPointeOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<CardPointeService> _logger;

    public CardPointeService(
        HttpClient httpClient,
        IOptions<CardPointeOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<CardPointeService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public PaymentConfig GetPublicConfig() => new()
    {
        MerchantId = _options.MerchantId,
        TokenizerUrl = _options.TokenizerUrl,
        Environment = _options.IsProduction ? "production" : "test",
        IsTestMode = !_options.IsProduction,
        Currency = _options.Currency,
        MinAmountCents = _options.MinAmountCents,
        MaxAmountCents = _options.MaxAmountCents
    };

    public async Task<CardPointeResponse> ChargeAsync(CardPointeChargeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Failed("Error", "A card token is required.");
        }

        if (_options.UseMockApi)
        {
            if (!_hostEnvironment.IsProduction())
            {
                return MockApproval(request);
            }

            _logger.LogError("CardPointe:UseMockApi is set in Production and was ignored");
        }

        if (!_options.HasCredentials)
        {
            _logger.LogError(
                "CardPointe is not configured (MerchantId set: {HasMerchant}, ApiUsername set: {HasUser}, ApiPassword set: {HasPassword}, ApiUrl set: {HasUrl})",
                !string.IsNullOrWhiteSpace(_options.MerchantId),
                !string.IsNullOrWhiteSpace(_options.ApiUsername),
                !string.IsNullOrWhiteSpace(_options.ApiPassword),
                !string.IsNullOrWhiteSpace(_options.ApiUrl));
            return Failed("Error", "Card payments are not available right now.");
        }

        var body = new AuthRequest
        {
            MerchId = _options.MerchantId,
            Account = request.Token,
            Expiry = request.Expiry,
            Amount = request.AmountCents.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Currency = request.Currency,
            OrderId = request.OrderId,
            Name = request.CardholderName,
            Email = request.Email,
            Capture = "Y",   // authorize and capture: a sale
            EcomInd = "E"    // e-commerce transaction
        };

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _options.ApiUrl.TrimEnd('/') + "/auth")
        {
            Content = JsonContent.Create(body)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiUsername}:{_options.ApiPassword}")));
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        _logger.LogInformation(
            "CardPointe sale: order {OrderId}, {AmountCents} cents {Currency}, token {Token}",
            request.OrderId, request.AmountCents, request.Currency, MaskToken(request.Token));

        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError("CardPointe rejected the API credentials (401) for merchant {MerchantId}", _options.MerchantId);
                }
                else
                {
                    _logger.LogError("CardPointe returned HTTP {StatusCode} for order {OrderId}", (int)response.StatusCode, request.OrderId);
                }

                return Failed("Error", "The payment could not be processed. Please try again.");
            }

            return Parse(content, request.OrderId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            // The sale may still have gone through. Look it up by order ID (inquireByOrderid) before retrying.
            _logger.LogError("CardPointe timed out for order {OrderId}; the charge's outcome is unknown", request.OrderId);
            return new CardPointeResponse
            {
                StatusMessage = "Error",
                OutcomeUnknown = true,
                ErrorMessage = "The payment gateway did not answer in time. Check before trying again."
            };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach CardPointe for order {OrderId}", request.OrderId);
            return Failed("Error", "The payment gateway could not be reached. Please try again.");
        }
    }

    private CardPointeResponse Parse(string content, string orderId)
    {
        AuthResponse? auth;
        try
        {
            auth = JsonSerializer.Deserialize<AuthResponse>(content);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "CardPointe answer for order {OrderId} was not valid JSON", orderId);
            auth = null;
        }

        if (auth is null)
        {
            return Failed("Error", "The payment gateway's answer could not be read.");
        }

        // respstat: A = approved, B = retry, C = declined.
        var result = auth.RespStat switch
        {
            "A" => new CardPointeResponse
            {
                IsSuccess = true,
                StatusMessage = "Approved",
                TransactionId = auth.RetRef,
                AuthorizationCode = auth.AuthCode
            },
            "B" => new CardPointeResponse
            {
                StatusMessage = "Retry",
                TransactionId = auth.RetRef,
                ErrorMessage = "The payment could not be completed. Please try again."
            },
            _ => new CardPointeResponse
            {
                StatusMessage = "Declined",
                TransactionId = auth.RetRef,
                ErrorMessage = string.IsNullOrWhiteSpace(auth.RespText) ? "The card was declined." : auth.RespText
            }
        };

        _logger.LogInformation(
            "CardPointe order {OrderId}: {Status} (respstat {RespStat}, respcode {RespCode}, retref {RetRef})",
            orderId, result.StatusMessage, auth.RespStat, auth.RespCode, auth.RetRef);

        return result;
    }

    private CardPointeResponse MockApproval(CardPointeChargeRequest request)
    {
        var retRef = $"MOCK{DateTime.UtcNow:yyMMddHHmmssfff}";
        _logger.LogWarning("CardPointe mock approved order {OrderId} ({AmountCents} cents) without calling the gateway", request.OrderId, request.AmountCents);
        return new CardPointeResponse
        {
            IsSuccess = true,
            StatusMessage = "Approved",
            TransactionId = retRef,
            AuthorizationCode = "MOCK00"
        };
    }

    private static CardPointeResponse Failed(string status, string message) =>
        new() { IsSuccess = false, StatusMessage = status, ErrorMessage = message };

    private static string MaskToken(string token) =>
        token.Length <= 4 ? "****" : "****" + token[^4..];

    private sealed class AuthRequest
    {
        [JsonPropertyName("merchid")] public required string MerchId { get; init; }
        [JsonPropertyName("account")] public required string Account { get; init; }
        [JsonPropertyName("expiry")] public required string Expiry { get; init; }
        [JsonPropertyName("amount")] public required string Amount { get; init; }
        [JsonPropertyName("currency")] public required string Currency { get; init; }
        [JsonPropertyName("orderid")] public required string OrderId { get; init; }
        [JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; init; }
        [JsonPropertyName("email"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Email { get; init; }
        [JsonPropertyName("capture")] public required string Capture { get; init; }
        [JsonPropertyName("ecomind")] public required string EcomInd { get; init; }
    }

    private sealed class AuthResponse
    {
        [JsonPropertyName("respstat")] public string? RespStat { get; init; }
        [JsonPropertyName("respcode")] public string? RespCode { get; init; }
        [JsonPropertyName("resptext")] public string? RespText { get; init; }
        [JsonPropertyName("retref")] public string? RetRef { get; init; }
        [JsonPropertyName("authcode")] public string? AuthCode { get; init; }
    }
}
