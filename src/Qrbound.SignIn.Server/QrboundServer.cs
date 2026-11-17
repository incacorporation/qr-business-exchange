using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Qrbound.SignIn.Server;

/// <summary>Settings for <c>AddQrboundServer(...)</c>.</summary>
public class QrboundServerOptions
{
    /// <summary>Base URL of the qrbound API, e.g. <c>https://webapi.qrbound.com</c>.</summary>
    public string Authority { get; set; } = "https://webapi.qrbound.com";

    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Client secret of a confidential application. Load it from user secrets, environment variables or a
    /// vault, never from source. Leave null for public (PKCE-only) applications.
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>The redirect URI the code was issued for (must match exactly).</summary>
    public string RedirectUri { get; set; } = string.Empty;

    /// <summary>Expected <c>iss</c>. Defaults to <see cref="Authority"/>.</summary>
    public string? Issuer { get; set; }

    /// <summary>Discovery document. Defaults to <c>{Authority}/.well-known/qr-sso-configuration</c>.</summary>
    public string? MetadataAddress { get; set; }

    /// <summary>Only allow https metadata. Turn off only for local development.</summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>
    /// Cookie scheme to sign the user in with after the exchange (e.g. <c>CookieAuthenticationDefaults.AuthenticationScheme</c>).
    /// Null: the exchange endpoint only returns the user.
    /// </summary>
    public string? SignInScheme { get; set; }

    /// <summary>Keep the qrbound access and ID tokens in the authentication cookie.</summary>
    public bool SaveTokens { get; set; }

    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(1);

    internal string AuthorityTrimmed => Authority.TrimEnd('/');
    internal string ExpectedIssuer => (Issuer ?? Authority).TrimEnd('/');
    internal string Metadata => MetadataAddress ?? $"{AuthorityTrimmed}/.well-known/qr-sso-configuration";
}

/// <summary>
/// Validates qrbound tokens (RS256 signature against qrbound's JWKS, issuer, audience = your client_id,
/// lifetime, token_use, nonce).
/// </summary>
public class QrboundTokenValidator
{
    private readonly IConfigurationManager<OpenIdConnectConfiguration> _configuration;
    private readonly QrboundServerOptions _options;
    private readonly JsonWebTokenHandler _handler = new();

    public QrboundTokenValidator(IConfigurationManager<OpenIdConnectConfiguration> configuration,
        IOptions<QrboundServerOptions> options)
    {
        _configuration = configuration;
        _options = options.Value;
    }

    /// <summary>Validates an ID token from a sign-in you started with <paramref name="expectedNonce"/>.</summary>
    public Task<ClaimsPrincipal> ValidateIdTokenAsync(string idToken, string? expectedNonce, CancellationToken cancellationToken = default) =>
        ValidateAsync(idToken, "id", expectedNonce, cancellationToken);

    /// <summary>Validates an access token sent to your API by your own front end.</summary>
    public Task<ClaimsPrincipal> ValidateAccessTokenAsync(string accessToken, CancellationToken cancellationToken = default) =>
        ValidateAsync(accessToken, "access", null, cancellationToken);

    private async Task<ClaimsPrincipal> ValidateAsync(string token, string tokenUse, string? expectedNonce,
        CancellationToken cancellationToken)
    {
        var result = await ValidateWithKeysAsync(token, cancellationToken);
        if (!result.IsValid && result.Exception is SecurityTokenSignatureKeyNotFoundException)
        {
            // qrbound may have rotated its key: refresh once and retry.
            _configuration.RequestRefresh();
            result = await ValidateWithKeysAsync(token, cancellationToken);
        }

        if (!result.IsValid)
        {
            throw new QrboundTokenException($"The token is not valid: {result.Exception?.Message}");
        }

        var identity = result.ClaimsIdentity;
        if (identity.FindFirst("token_use")?.Value != tokenUse)
        {
            throw new QrboundTokenException($"A qrbound {tokenUse} token is required.");
        }

        if (!string.IsNullOrEmpty(expectedNonce) &&
            !string.Equals(identity.FindFirst("nonce")?.Value, expectedNonce, StringComparison.Ordinal))
        {
            throw new QrboundTokenException("The token nonce does not match this sign-in.");
        }

        return new ClaimsPrincipal(new ClaimsIdentity(identity.Claims, "qrbound", "name", "business_role"));
    }

    private async Task<TokenValidationResult> ValidateWithKeysAsync(string token, CancellationToken cancellationToken)
    {
        var config = await _configuration.GetConfigurationAsync(cancellationToken);
        return await _handler.ValidateTokenAsync(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = new[] { _options.ExpectedIssuer, _options.ExpectedIssuer + "/" },
            ValidateAudience = true,
            ValidAudience = _options.ClientId,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            IssuerSigningKeys = config.SigningKeys,
            ValidAlgorithms = new[] { SecurityAlgorithms.RsaSha256 },
            ClockSkew = _options.ClockSkew
        });
    }
}

/// <summary>Exchanges authorization codes server-to-server (with the client secret when configured).</summary>
public class QrboundServerClient
{
    public const string HttpClientName = "Qrbound.Server";

    private readonly HttpClient _http;
    private readonly QrboundServerOptions _options;

    public QrboundServerClient(IHttpClientFactory factory, IOptions<QrboundServerOptions> options)
    {
        _http = factory.CreateClient(HttpClientName);
        _options = options.Value;
    }

    public async Task<QrboundTokenResponse> ExchangeCodeAsync(string code, string? codeVerifier,
        CancellationToken cancellationToken = default)
    {
        var fields = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _options.RedirectUri,
            ["client_id"] = _options.ClientId
        };
        if (!string.IsNullOrEmpty(codeVerifier)) fields["code_verifier"] = codeVerifier;
        if (!string.IsNullOrEmpty(_options.ClientSecret)) fields["client_secret"] = _options.ClientSecret;

        using var response = await _http.PostAsync($"{_options.AuthorityTrimmed}/api/sso/token",
            new FormUrlEncodedContent(fields), cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            string? error = null, description = null;
            try
            {
                var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string?>>(cancellationToken);
                body?.TryGetValue("error", out error);
                body?.TryGetValue("error_description", out description);
            }
            catch (Exception) { /* not JSON */ }
            throw new QrboundException(error ?? "http_error", description ?? $"Token endpoint returned {(int)response.StatusCode}.", (int)response.StatusCode);
        }

        return await response.Content.ReadFromJsonAsync<QrboundTokenResponse>(cancellationToken)
            ?? throw new QrboundException("invalid_response", "Empty token response.");
    }
}

public static class QrboundServerExtensions
{
    /// <summary>Registers <see cref="QrboundTokenValidator"/> and <see cref="QrboundServerClient"/>.</summary>
    public static IServiceCollection AddQrboundServer(this IServiceCollection services, Action<QrboundServerOptions> configure)
    {
        services.AddOptions<QrboundServerOptions>().Configure(configure).Validate(o =>
            Uri.TryCreate(o.Authority, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(o.ClientId) &&
            Uri.TryCreate(o.RedirectUri, UriKind.Absolute, out _),
            "Qrbound: Authority, ClientId and RedirectUri are required.");

        services.AddHttpClient(QrboundServerClient.HttpClientName);
        services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<QrboundServerOptions>>().Value;
            var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient(QrboundServerClient.HttpClientName);
            return new ConfigurationManager<OpenIdConnectConfiguration>(options.Metadata,
                new OpenIdConnectConfigurationRetriever(),
                new HttpDocumentRetriever(http) { RequireHttps = options.RequireHttpsMetadata });
        });
        services.AddSingleton<QrboundTokenValidator>();
        services.AddTransient<QrboundServerClient>();
        return services;
    }

    /// <summary>
    /// Maps <c>POST {prefix}/exchange</c> (used by the components in Backend mode) and <c>POST {prefix}/signout</c>.
    /// The exchange endpoint only accepts JSON, so other sites cannot post to it with a plain form.
    /// </summary>
    public static IEndpointConventionBuilder MapQrboundSignIn(this IEndpointRouteBuilder endpoints, string prefix = "/qrbound")
    {
        var group = endpoints.MapGroup(prefix);

        group.MapPost("/exchange", async (HttpContext http, QrboundBackendExchangeRequest request,
            QrboundServerClient client, QrboundTokenValidator validator, IOptions<QrboundServerOptions> options,
            ILoggerFactory loggers) =>
        {
            var logger = loggers.CreateLogger("Qrbound.SignIn.Server");
            if (string.IsNullOrEmpty(request.Code) || !Pkce.IsValidVerifier(request.CodeVerifier))
            {
                return Results.BadRequest(new { error = "invalid_request", error_description = "code and a valid code_verifier are required" });
            }

            QrboundTokenResponse tokens;
            ClaimsPrincipal principal;
            try
            {
                tokens = await client.ExchangeCodeAsync(request.Code, request.CodeVerifier, http.RequestAborted);
                principal = await validator.ValidateIdTokenAsync(tokens.IdToken, request.Nonce, http.RequestAborted);
            }
            catch (QrboundException ex)
            {
                logger.LogInformation("qrbound code exchange rejected: {Error}", ex.Error);
                return Results.BadRequest(new { error = ex.Error, error_description = ex.Message });
            }
            catch (QrboundTokenException ex)
            {
                logger.LogWarning("qrbound ID token rejected: {Message}", ex.Message);
                return Results.BadRequest(new { error = "invalid_token", error_description = "The sign-in could not be verified." });
            }

            var user = ToUser(principal);
            var o = options.Value;
            if (o.SignInScheme != null)
            {
                var properties = new AuthenticationProperties { ExpiresUtc = user.ExpiresAt, IsPersistent = false };
                if (o.SaveTokens)
                {
                    properties.StoreTokens(new[]
                    {
                        new AuthenticationToken { Name = "access_token", Value = tokens.AccessToken },
                        new AuthenticationToken { Name = "id_token", Value = tokens.IdToken }
                    });
                }
                await http.SignInAsync(o.SignInScheme, principal, properties);
            }

            http.Response.Headers.CacheControl = "no-store";
            return Results.Ok(QrboundBackendUser.From(user));
        });

        group.MapPost("/signout", async (HttpContext http, IOptions<QrboundServerOptions> options) =>
        {
            if (options.Value.SignInScheme != null) await http.SignOutAsync(options.Value.SignInScheme);
            return Results.NoContent();
        });

        return group;
    }

    internal static QrboundUser ToUser(ClaimsPrincipal principal)
    {
        string? Get(string type) => principal.FindFirst(type)?.Value;
        var exp = long.TryParse(Get("exp"), out var e) ? DateTimeOffset.FromUnixTimeSeconds(e) : DateTimeOffset.UtcNow.AddHours(1);
        return new QrboundUser
        {
            Subject = Get("sub") ?? string.Empty,
            Email = Get("email"),
            EmailVerified = string.Equals(Get("email_verified"), "true", StringComparison.OrdinalIgnoreCase),
            Name = Get("name"),
            GivenName = Get("given_name"),
            FamilyName = Get("family_name"),
            AuthMethod = Get("amr"),
            BusinessId = Guid.TryParse(Get("business_id"), out var b) ? b : null,
            BusinessSlug = Get("business_slug"),
            BusinessRole = Get("business_role"),
            ExpiresAt = exp,
            Claims = principal.Claims.ToList()
        };
    }
}
