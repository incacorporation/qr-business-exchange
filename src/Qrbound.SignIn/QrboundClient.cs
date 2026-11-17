using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace Qrbound.SignIn;

/// <summary>
/// Talks to qrbound for the sign-in components. Use it directly to build your own UI.
/// </summary>
public class QrboundClient
{
    /// <summary>Name of the <see cref="HttpClient"/> registered for qrbound calls.</summary>
    public const string HttpClientName = "Qrbound";

    private readonly HttpClient _http;
    private readonly IHttpClientFactory _factory;
    private readonly QrboundOptions _options;
    private readonly IQrboundStateStore _stateStore;
    private readonly NavigationManager? _navigation;
    private readonly TimeProvider _time;

    public QrboundClient(IHttpClientFactory factory, IOptions<QrboundOptions> options,
        IQrboundStateStore stateStore, NavigationManager? navigation = null, TimeProvider? time = null)
    {
        _factory = factory;
        _http = factory.CreateClient(HttpClientName);
        _options = options.Value;
        _stateStore = stateStore;
        _navigation = navigation;
        _time = time ?? TimeProvider.System;
    }

    public QrboundOptions Options => _options;

    // ------------------------------------------------------------------ QR flow

    /// <summary>Starts a QR sign-in bound to this app's client_id, redirect URI and a fresh PKCE pair.</summary>
    public async Task<QrboundQrSession> StartQrSignInAsync(QrboundMode mode = QrboundMode.SignIn,
        CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync("api/qr-login/sessions",
            new QrSessionCreateRequest { ClientId = _options.ClientId, RedirectUri = _options.RedirectUri },
            cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var created = await response.Content.ReadFromJsonAsync<QrSessionCreateResponse>(cancellationToken)
            ?? throw new QrboundException("invalid_response", "qrbound returned an empty response.");

        var verifier = Pkce.CreateVerifier();
        return new QrboundQrSession
        {
            SessionId = created.SessionId,
            ApproveUrl = mode == QrboundMode.Register ? AppendQuery(created.ApproveUrl, "register", "1") : created.ApproveUrl,
            VerificationCode = created.VerificationCode,
            ExpiresAt = new DateTimeOffset(DateTime.SpecifyKind(created.ExpiresAt, DateTimeKind.Utc)),
            PollInterval = created.PollIntervalSeconds > 0 ? TimeSpan.FromSeconds(created.PollIntervalSeconds) : _options.PollInterval,
            Mode = mode,
            PollSecret = created.PollSecret,
            CodeVerifier = verifier,
            CodeChallenge = Pkce.CreateChallenge(verifier),
            Nonce = Pkce.CreateRandomValue(),
            State = Pkce.CreateRandomValue()
        };
    }

    /// <summary>
    /// Asks whether the phone approved. When it has, the result carries a one-time authorization code
    /// bound to this session's PKCE challenge.
    /// </summary>
    public async Task<QrboundPollResult> PollAsync(QrboundQrSession session, CancellationToken cancellationToken = default)
    {
        var response = await _http.PostAsJsonAsync($"api/qr-login/sessions/{session.SessionId}/authorize",
            new QrSessionAuthorizeRequest
            {
                PollSecret = session.PollSecret,
                ClientId = _options.ClientId,
                RedirectUri = _options.RedirectUri,
                CodeChallenge = session.CodeChallenge,
                Nonce = session.Nonce,
                State = session.State
            }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return new QrboundPollResult { Status = QrboundStatus.Expired, ExpiresAt = session.ExpiresAt };
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var body = await response.Content.ReadFromJsonAsync<QrSessionAuthorizeResponse>(cancellationToken)
            ?? throw new QrboundException("invalid_response", "qrbound returned an empty response.");

        if (body.Status == QrboundStatus.Approved && !string.Equals(body.State, session.State, StringComparison.Ordinal))
        {
            throw new QrboundException("invalid_state", "The sign-in response did not match this request.");
        }

        return new QrboundPollResult
        {
            Status = body.Status,
            ExpiresAt = new DateTimeOffset(DateTime.SpecifyKind(body.ExpiresAt, DateTimeKind.Utc)),
            Code = body.Status == QrboundStatus.Approved ? body.Code : null
        };
    }

    /// <summary>Exchanges the code from an approved QR session.</summary>
    public Task<QrboundSignInResult> CompleteAsync(QrboundQrSession session, string code,
        CancellationToken cancellationToken = default) =>
        ExchangeAsync(code, session.CodeVerifier, session.Nonce, null, cancellationToken);

    // ------------------------------------------------------------------ redirect flow

    /// <summary>
    /// Builds the URL of qrbound's sign-in page (passkey or QR) for a full-page redirect.
    /// The PKCE verifier, state and nonce are kept in the <see cref="IQrboundStateStore"/> until
    /// <see cref="HandleCallbackAsync"/> runs on the redirect URI.
    /// </summary>
    public async Task<string> BuildAuthorizeUrlAsync(string? returnUrl = null, bool forceLogin = false)
    {
        var pending = new QrboundPendingRedirect
        {
            State = Pkce.CreateRandomValue(),
            Nonce = Pkce.CreateRandomValue(),
            CodeVerifier = Pkce.CreateVerifier(),
            ReturnUrl = returnUrl,
            CreatedAt = _time.GetUtcNow()
        };
        await _stateStore.SaveAsync(pending);

        return BuildAuthorizeUrl(_options, pending.State, pending.Nonce, Pkce.CreateChallenge(pending.CodeVerifier), forceLogin);
    }

    /// <summary>Pure URL builder for the redirect flow.</summary>
    public static string BuildAuthorizeUrl(QrboundOptions options, string state, string nonce, string codeChallenge,
        bool forceLogin = false)
    {
        var query = new List<KeyValuePair<string, string>>
        {
            new("client_id", options.ClientId),
            new("redirect_uri", options.RedirectUri),
            new("response_type", "code"),
            new("state", state),
            new("nonce", nonce),
            new("code_challenge", codeChallenge),
            new("code_challenge_method", Pkce.MethodS256)
        };
        if (forceLogin) query.Add(new("prompt", "login"));

        return $"{options.PortalBaseUrl.TrimEnd('/')}/sso/authorize?" +
               string.Join("&", query.Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value)}"));
    }

    /// <summary>Completes a redirect sign-in on the redirect URI page.</summary>
    public async Task<QrboundSignInResult> HandleCallbackAsync(Uri callbackUri, CancellationToken cancellationToken = default)
    {
        var query = ParseQuery(callbackUri.Query);
        query.TryGetValue("state", out var state);

        if (query.TryGetValue("error", out var error))
        {
            if (!string.IsNullOrEmpty(state)) await _stateStore.TakeAsync(state);
            query.TryGetValue("error_description", out var description);
            throw new QrboundException(error, description);
        }

        if (!query.TryGetValue("code", out var code) || string.IsNullOrEmpty(state))
        {
            throw new QrboundException("invalid_request", "The callback is missing code or state.");
        }

        var pending = await _stateStore.TakeAsync(state)
            ?? throw new QrboundException("invalid_state", "This sign-in was not started here, or it was already completed.");

        if (_time.GetUtcNow() - pending.CreatedAt > TimeSpan.FromMinutes(15))
        {
            throw new QrboundException("invalid_state", "The sign-in took too long. Please try again.");
        }

        return await ExchangeAsync(code, pending.CodeVerifier, pending.Nonce, pending.ReturnUrl, cancellationToken);
    }

    // ------------------------------------------------------------------ token exchange

    private async Task<QrboundSignInResult> ExchangeAsync(string code, string verifier, string nonce, string? returnUrl,
        CancellationToken cancellationToken)
    {
        if (_options.TokenExchange == QrboundTokenExchange.Backend)
        {
            var backend = _factory.CreateClient(HttpClientName + ".Backend");
            var url = ResolveBackendUrl();
            var response = await backend.PostAsJsonAsync(url,
                new QrboundBackendExchangeRequest { Code = code, CodeVerifier = verifier, Nonce = nonce }, cancellationToken);
            await EnsureSuccessAsync(response, cancellationToken);
            var user = await response.Content.ReadFromJsonAsync<QrboundBackendUser>(cancellationToken)
                ?? throw new QrboundException("invalid_response", "The backend returned an empty response.");
            return new QrboundSignInResult { User = user.ToUser(), ReturnUrl = returnUrl };
        }

        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
            ["client_id"] = _options.ClientId,
            ["code_verifier"] = verifier
        });

        var tokenResponse = await _http.PostAsync("api/sso/token", form, cancellationToken);
        await EnsureSuccessAsync(tokenResponse, cancellationToken);
        var tokens = await tokenResponse.Content.ReadFromJsonAsync<QrboundTokenResponse>(cancellationToken)
            ?? throw new QrboundException("invalid_response", "qrbound returned an empty token response.");

        QrboundUser signedIn;
        try
        {
            signedIn = QrboundIdTokenReader.Read(tokens.IdToken, _options.ExpectedIssuer, _options.ClientId,
                nonce, _time.GetUtcNow(), _options.ClockSkew);
        }
        catch (QrboundTokenException ex)
        {
            throw new QrboundException("invalid_token", ex.Message);
        }

        return new QrboundSignInResult { User = signedIn, Tokens = tokens, ReturnUrl = returnUrl };
    }

    private string ResolveBackendUrl()
    {
        var path = _options.BackendExchangePath;
        if (Uri.TryCreate(path, UriKind.Absolute, out var absolute) && absolute.Scheme.StartsWith("http"))
            return absolute.ToString();
        if (_navigation == null)
            throw new InvalidOperationException("Qrbound: BackendExchangePath must be absolute when no NavigationManager is available.");
        return _navigation.ToAbsoluteUri(path.TrimStart('/')).ToString();
    }

    // ------------------------------------------------------------------ helpers

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode) return;

        OAuthError? error = null;
        try
        {
            error = await response.Content.ReadFromJsonAsync<OAuthError>(cancellationToken);
        }
        catch (Exception) { /* not JSON */ }

        throw new QrboundException(
            error?.Error ?? (response.StatusCode == HttpStatusCode.TooManyRequests ? "rate_limited" : "http_error"),
            error?.ErrorDescription ?? $"qrbound responded with {(int)response.StatusCode} {response.ReasonPhrase}.",
            (int)response.StatusCode);
    }

    internal static string AppendQuery(string url, string key, string value)
    {
        var fragmentIndex = url.IndexOf('#');
        var fragment = fragmentIndex >= 0 ? url[fragmentIndex..] : string.Empty;
        var head = fragmentIndex >= 0 ? url[..fragmentIndex] : url;
        var separator = head.Contains('?') ? '&' : '?';
        return $"{head}{separator}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}{fragment}";
    }

    internal static Dictionary<string, string> ParseQuery(string query)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            var key = Uri.UnescapeDataString((eq < 0 ? part : part[..eq]).Replace('+', ' '));
            var value = eq < 0 ? string.Empty : Uri.UnescapeDataString(part[(eq + 1)..].Replace('+', ' '));
            result.TryAdd(key, value);
        }
        return result;
    }
}
