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

        if (request.UsesProfile ? string.IsNullOrWhiteSpace(request.AccountId) : string.IsNullOrWhiteSpace(request.Token))
        {
            return Failed("Error", "A card token or a card on file is required.");
        }

        if (UseMock())
        {
            return MockApproval(request);
        }

        if (!IsConfigured())
        {
            return Failed("Error", "Card payments are not available right now.");
        }

        var body = new AuthRequest
        {
            MerchId = _options.MerchantId,
            Amount = request.AmountCents.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Currency = request.Currency,
            OrderId = request.OrderId,
            Name = request.CardholderName,
            Email = request.Email,
            Capture = "Y",   // authorize and capture: a sale
            EcomInd = "E"    // e-commerce transaction
        };

        if (request.UsesProfile)
        {
            // Card on file, used by the cardholder right now (customer-initiated, not a scheduled charge).
            body.Profile = $"{request.ProfileId}/{request.AccountId}";
            body.Cof = "C";
            body.CofScheduled = "N";
        }
        else
        {
            body.Account = request.Token;
            body.Expiry = request.Expiry;
        }

        using var httpRequest = NewRequest(HttpMethod.Post, "auth", body);

        _logger.LogInformation(
            "CardPointe sale: order {OrderId}, {AmountCents} cents {Currency}, {Card}",
            request.OrderId, request.AmountCents, request.Currency,
            request.UsesProfile ? $"card on file {request.ProfileId}/{request.AccountId}" : $"token {MaskToken(request.Token!)}");

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

    // Cards on file --------------------------------------------------------------

    public async Task<CardPointeProfileResult> CreateProfileAsync(CardPointeProfileRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.Expiry))
        {
            return new CardPointeProfileResult { ErrorMessage = "A card token and expiry are required." };
        }

        if (UseMock())
        {
            var mockProfile = Random.Shared.NextInt64(10_000_000_000_000_000, 99_999_999_999_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _logger.LogWarning("CardPointe mock saved card {Token} as profile {ProfileId} without calling the gateway", MaskToken(request.Token), mockProfile);
            return new CardPointeProfileResult
            {
                IsSuccess = true,
                ProfileId = mockProfile,
                AccountId = "1",
                Last4 = Last4(request.Token),
                CardBrand = BrandFromToken(request.Token)
            };
        }

        if (!IsConfigured())
        {
            return new CardPointeProfileResult { ErrorMessage = "Saving cards isn't available right now." };
        }

        var body = new ProfileRequest
        {
            MerchId = _options.MerchantId,
            Account = request.Token,
            Expiry = request.Expiry,
            Name = request.CardholderName,
            DefaultAcct = "Y"
        };

        using var httpRequest = NewRequest(HttpMethod.Put, "profile", body);
        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("CardPointe returned HTTP {StatusCode} saving card {Token}", (int)response.StatusCode, MaskToken(request.Token));
                return new CardPointeProfileResult { ErrorMessage = "The card couldn't be saved. Please try again." };
            }

            var profile = JsonSerializer.Deserialize<ProfileResponse>(content);
            if (profile is null || profile.RespStat != "A" || string.IsNullOrWhiteSpace(profile.ProfileId) || string.IsNullOrWhiteSpace(profile.AcctId))
            {
                _logger.LogWarning("CardPointe didn't save card {Token}: respstat {RespStat}, {RespText}",
                    MaskToken(request.Token), profile?.RespStat, profile?.RespText);
                return new CardPointeProfileResult
                {
                    ErrorMessage = string.IsNullOrWhiteSpace(profile?.RespText) ? "The card couldn't be saved." : profile.RespText
                };
            }

            var token = profile.Token ?? request.Token;
            _logger.LogInformation("CardPointe saved card {Token} as profile {ProfileId}/{AcctId}", MaskToken(token), profile.ProfileId, profile.AcctId);
            return new CardPointeProfileResult
            {
                IsSuccess = true,
                ProfileId = profile.ProfileId,
                AccountId = profile.AcctId,
                Last4 = Last4(token),
                CardBrand = string.IsNullOrWhiteSpace(profile.AcctType) ? BrandFromToken(token) : profile.AcctType
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Saving card {Token} with CardPointe failed", MaskToken(request.Token));
            return new CardPointeProfileResult { ErrorMessage = "The card couldn't be saved. Please try again." };
        }
    }

    public async Task<bool> DeleteProfileAsync(string profileId, string accountId, CancellationToken cancellationToken = default)
    {
        if (UseMock())
        {
            _logger.LogWarning("CardPointe mock deleted profile {ProfileId}/{AcctId} without calling the gateway", profileId, accountId);
            return true;
        }

        if (!IsConfigured())
        {
            return false;
        }

        var path = $"profile/{Uri.EscapeDataString(profileId)}/{Uri.EscapeDataString(accountId)}/{Uri.EscapeDataString(_options.MerchantId)}";
        using var httpRequest = NewRequest(HttpMethod.Delete, path, null);
        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var result = response.IsSuccessStatusCode ? JsonSerializer.Deserialize<ProfileResponse>(content) : null;
            if (result?.RespStat == "A")
            {
                _logger.LogInformation("CardPointe deleted profile {ProfileId}/{AcctId}", profileId, accountId);
                return true;
            }

            _logger.LogWarning("CardPointe didn't delete profile {ProfileId}/{AcctId}: HTTP {StatusCode}, {RespText}",
                profileId, accountId, (int)response.StatusCode, result?.RespText);
            return false;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Deleting CardPointe profile {ProfileId}/{AcctId} failed", profileId, accountId);
            return false;
        }
    }

    // Hosted Payment Page ------------------------------------------------------------

    public Uri? GetHostedPageUrl(Uri apiBase)
    {
        if (Uri.TryCreate(_options.HostedPageUrl, UriKind.Absolute, out var url) && url.Scheme == Uri.UriSchemeHttps)
        {
            return url;
        }

        // Development only: the API's stand-in for the hosted page (see MockHostedPageController).
        return string.IsNullOrWhiteSpace(_options.HostedPageUrl) && UseMock()
            ? new Uri(apiBase, MockHostedPayments.PagePath)
            : null;
    }

    public async Task<CardPointeInquiry> InquireByOrderIdAsync(string orderId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return new CardPointeInquiry();
        }

        if (UseMock())
        {
            return new CardPointeInquiry { Reached = true, Transactions = MockHostedPayments.For(orderId) };
        }

        if (!IsConfigured())
        {
            return new CardPointeInquiry();
        }

        // set=1 asks for every transaction with this order ID (the hosted page can take several tries).
        var path = $"inquireByOrderid/{Uri.EscapeDataString(orderId)}/{Uri.EscapeDataString(_options.MerchantId)}/1";
        using var httpRequest = NewRequest(HttpMethod.Get, path, null);
        try
        {
            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("CardPointe inquiry for order {OrderId} returned HTTP {StatusCode}", orderId, (int)response.StatusCode);
                return new CardPointeInquiry();
            }

            var transactions = ParseInquiry(content)
                .Where(t => !string.IsNullOrWhiteSpace(t.RetRef))
                .Select(t => new CardPointeTransaction
                {
                    RetRef = t.RetRef!,
                    AuthCode = t.AuthCode,
                    AmountCents = ParseAmountCents(t.Amount),
                    RespStat = t.RespStat,
                    SetlStat = t.SetlStat,
                    Name = t.Name
                })
                .ToList();

            _logger.LogInformation("CardPointe inquiry for order {OrderId}: {Count} transaction(s), {Approved} approved",
                orderId, transactions.Count, transactions.Count(t => t.IsApproved));
            return new CardPointeInquiry { Reached = true, Transactions = transactions };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "CardPointe inquiry for order {OrderId} failed", orderId);
            return new CardPointeInquiry();
        }
    }

    /// <summary>The inquiry answers with one transaction or, with set=1, an array of them.</summary>
    private static List<InquiryResponse> ParseInquiry(string content)
    {
        var trimmed = content.TrimStart();
        if (trimmed.StartsWith('['))
        {
            return JsonSerializer.Deserialize<List<InquiryResponse>>(content) ?? [];
        }

        var single = JsonSerializer.Deserialize<InquiryResponse>(content);
        return single is null ? [] : [single];
    }

    /// <summary>
    /// CardPointe amounts with a decimal point are in dollars ("12.34"); without one they're in cents ("1234").
    /// </summary>
    public static long ParseAmountCents(string? amount)
    {
        if (string.IsNullOrWhiteSpace(amount) ||
            !decimal.TryParse(amount, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var value))
        {
            return 0;
        }

        return amount.Contains('.') ? (long)Math.Round(value * 100m, MidpointRounding.AwayFromZero) : (long)value;
    }

    // ---------------------------------------------------------------------------

    private bool UseMock()
    {
        if (!_options.UseMockApi)
        {
            return false;
        }

        if (!_hostEnvironment.IsProduction())
        {
            return true;
        }

        _logger.LogError("CardPointe:UseMockApi is set in Production and was ignored");
        return false;
    }

    private bool IsConfigured()
    {
        if (_options.HasCredentials)
        {
            return true;
        }

        _logger.LogError(
            "CardPointe is not configured (MerchantId set: {HasMerchant}, ApiUsername set: {HasUser}, ApiPassword set: {HasPassword}, ApiUrl set: {HasUrl})",
            !string.IsNullOrWhiteSpace(_options.MerchantId),
            !string.IsNullOrWhiteSpace(_options.ApiUsername),
            !string.IsNullOrWhiteSpace(_options.ApiPassword),
            !string.IsNullOrWhiteSpace(_options.ApiUrl));
        return false;
    }

    /// <summary>A request to {ApiUrl}/{path} with Basic auth. Never logged: it carries the credentials.</summary>
    private HttpRequestMessage NewRequest(HttpMethod method, string path, object? body)
    {
        var request = new HttpRequestMessage(method, _options.ApiUrl.TrimEnd('/') + "/" + path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, body.GetType());
        }

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_options.ApiUsername}:{_options.ApiPassword}")));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return request;
    }

    private static CardPointeResponse Failed(string status, string message) =>
        new() { IsSuccess = false, StatusMessage = status, ErrorMessage = message };

    private static string MaskToken(string token) =>
        token.Length <= 4 ? "****" : "****" + token[^4..];

    private static string Last4(string token) => token.Length >= 4 ? token[^4..] : token;

    /// <summary>CardSecure tokens start with 9 and keep the card's second digit: 94 Visa, 95 Mastercard, 93 Amex, 96 Discover.</summary>
    private static string? BrandFromToken(string token) =>
        token.Length > 2 && token[0] == '9'
            ? token[1] switch { '4' => "VISA", '5' => "MC", '3' => "AMEX", '6' => "DISC", _ => null }
            : null;

    private sealed class AuthRequest
    {
        [JsonPropertyName("merchid")] public required string MerchId { get; init; }
        [JsonPropertyName("account"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Account { get; set; }
        [JsonPropertyName("expiry"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Expiry { get; set; }
        [JsonPropertyName("profile"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Profile { get; set; }
        [JsonPropertyName("cof"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Cof { get; set; }
        [JsonPropertyName("cofscheduled"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? CofScheduled { get; set; }
        [JsonPropertyName("amount")] public required string Amount { get; init; }
        [JsonPropertyName("currency")] public required string Currency { get; init; }
        [JsonPropertyName("orderid")] public required string OrderId { get; init; }
        [JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; init; }
        [JsonPropertyName("email"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Email { get; init; }
        [JsonPropertyName("capture")] public required string Capture { get; init; }
        [JsonPropertyName("ecomind")] public required string EcomInd { get; init; }
    }

    private sealed class ProfileRequest
    {
        [JsonPropertyName("merchid")] public required string MerchId { get; init; }
        [JsonPropertyName("account")] public required string Account { get; init; }
        [JsonPropertyName("expiry")] public required string Expiry { get; init; }
        [JsonPropertyName("name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public string? Name { get; init; }
        [JsonPropertyName("defaultacct")] public required string DefaultAcct { get; init; }
    }

    private sealed class ProfileResponse
    {
        [JsonPropertyName("respstat")] public string? RespStat { get; init; }
        [JsonPropertyName("resptext")] public string? RespText { get; init; }
        [JsonPropertyName("profileid")] public string? ProfileId { get; init; }
        [JsonPropertyName("acctid")] public string? AcctId { get; init; }
        [JsonPropertyName("token")] public string? Token { get; init; }
        [JsonPropertyName("accttype")] public string? AcctType { get; init; }
    }

    private sealed class InquiryResponse
    {
        [JsonPropertyName("retref")] public string? RetRef { get; init; }
        [JsonPropertyName("authcode")] public string? AuthCode { get; init; }
        [JsonPropertyName("amount")] public string? Amount { get; init; }
        [JsonPropertyName("respstat")] public string? RespStat { get; init; }
        [JsonPropertyName("setlstat")] public string? SetlStat { get; init; }
        [JsonPropertyName("name")] public string? Name { get; init; }
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
