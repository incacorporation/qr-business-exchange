using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Single sign-on endpoints used by business applications (JWT + passkey only).
/// </summary>
/// <remarks>
/// Flow:
/// 1. App redirects the browser to {portal}/sso/authorize?client_id=..&amp;redirect_uri=..&amp;state=..&amp;code_challenge=..
/// 2. User signs in with a passkey or by approving a QR code on their phone.
/// 3. Portal calls POST /api/sso/authorize and redirects to redirect_uri?code=..&amp;state=..
/// 4. App calls POST /api/sso/token with the code and its client secret and/or PKCE verifier.
/// 5. App validates the RS256 JWT with the keys from GET /api/sso/jwks (aud = its client_id).
/// </remarks>
[ApiController]
[Route("api/sso")]
public class SsoController : ControllerBase
{
    private readonly SsoService _ssoService;
    private readonly SsoAccessService _accessService;
    private readonly ILogger<SsoController> _logger;

    public SsoController(SsoService ssoService, SsoAccessService accessService, ILogger<SsoController> logger)
    {
        _ssoService = ssoService;
        _accessService = accessService;
        _logger = logger;
    }

    /// <summary>
    /// Discovery document listing the SSO endpoints and supported features.
    /// </summary>
    [HttpGet("configuration")]
    [HttpGet("/.well-known/qr-sso-configuration")]
    [AllowAnonymous]
    public IActionResult GetConfiguration() =>
        Ok(_ssoService.GetConfiguration($"{Request.Scheme}://{Request.Host}{Request.PathBase}"));

    /// <summary>
    /// Public signing keys (JWKS) for validating tokens issued to applications.
    /// </summary>
    [HttpGet("jwks")]
    [AllowAnonymous]
    [ResponseCache(Duration = 3600)]
    public IActionResult GetJwks() => Ok(_ssoService.GetJwks());

    /// <summary>
    /// Public information about an application, used by the sign-in page.
    /// </summary>
    [HttpGet("clients/{clientId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SsoClientInfoDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetClient(string clientId, [FromQuery] string? redirectUri) =>
        Run(() => _ssoService.GetClientInfoAsync(clientId, redirectUri));

    /// <summary>
    /// Issues an authorization code for the signed-in portal user.
    /// The portal session must have been created with a passkey or QR approval.
    /// </summary>
    [HttpPost("authorize")]
    [Authorize]
    [ProducesResponseType(typeof(SsoAuthorizeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SsoErrorResponse), StatusCodes.Status403Forbidden)]
    public Task<IActionResult> Authorize([FromBody] SsoAuthorizeRequest request)
    {
        var userId = User.GetUserId();
        if (userId == null)
        {
            return Task.FromResult<IActionResult>(Unauthorized());
        }

        return Run(() => _ssoService.CreateAuthorizationCodeAsync(
            userId.Value, User.GetAuthMethod(), User.GetAuthTime(), request));
    }

    /// <summary>
    /// Where the signed-in user stands with one category of an application (e.g. "business"):
    /// never asked, pending, approved, rejected or revoked, plus the businesses they may act for.
    /// </summary>
    [HttpGet("access")]
    [Authorize]
    [ProducesResponseType(typeof(SsoCategoryAccessDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetAccess([FromQuery] string clientId, [FromQuery] string category)
    {
        var userId = User.GetUserId();
        return userId == null
            ? Task.FromResult<IActionResult>(Unauthorized())
            : Run(() => _accessService.GetCategoryAccessAsync(userId.Value, clientId, category));
    }

    /// <summary>
    /// The signed-in user asks to use an application as one of its categories. Granted at once when
    /// the category needs no approval; otherwise pending, and the application's administrators are emailed.
    /// </summary>
    [HttpPost("access-requests")]
    [Authorize]
    [ProducesResponseType(typeof(SsoCategoryAccessDto), StatusCodes.Status200OK)]
    public Task<IActionResult> RequestAccess([FromBody] SsoAccessRequest request)
    {
        var userId = User.GetUserId();
        return userId == null
            ? Task.FromResult<IActionResult>(Unauthorized())
            : Run(() => _accessService.RequestAsync(userId.Value, request));
    }

    /// <summary>
    /// Exchanges an authorization code for an access token and ID token.
    /// Accepts application/x-www-form-urlencoded (OAuth style) or JSON with the same snake_case fields:
    /// grant_type, code, redirect_uri, client_id, client_secret, code_verifier.
    /// </summary>
    [HttpPost("token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SsoTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SsoErrorResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Token()
    {
        Dictionary<string, string?> fields;
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync();
            fields = form.ToDictionary(k => k.Key, v => (string?)v.Value.ToString());
        }
        else
        {
            fields = await System.Text.Json.JsonSerializer.DeserializeAsync<Dictionary<string, string?>>(Request.Body)
                     ?? new Dictionary<string, string?>();
        }

        string? Get(string key) => fields.TryGetValue(key, out var v) ? v : null;

        // Also accept HTTP Basic client authentication
        var clientId = Get("client_id");
        var clientSecret = Get("client_secret");
        var authHeader = Request.Headers.Authorization.ToString();
        if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var decoded = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(authHeader[6..].Trim()));
                var separator = decoded.IndexOf(':');
                if (separator > 0)
                {
                    clientId = Uri.UnescapeDataString(decoded[..separator]);
                    clientSecret = Uri.UnescapeDataString(decoded[(separator + 1)..]);
                }
            }
            catch (FormatException)
            {
                return BadRequest(new SsoErrorResponse { Error = "invalid_client", ErrorDescription = "Malformed Basic credentials" });
            }
        }

        Response.Headers.CacheControl = "no-store";
        return await Run(() => _ssoService.ExchangeCodeAsync(
            Get("grant_type"), Get("code"), Get("redirect_uri"), clientId, clientSecret, Get("code_verifier")));
    }

    /// <summary>
    /// Returns the profile of the user an application access token was issued for.
    /// </summary>
    [HttpGet("userinfo")]
    [AllowAnonymous] // validated manually: these tokens are RS256 app tokens, not portal tokens
    public Task<IActionResult> UserInfo()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult<IActionResult>(Unauthorized(new SsoErrorResponse { Error = "invalid_token" }));
        }

        return Run(() => _ssoService.GetUserInfoAsync(header[7..].Trim()));
    }

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (SsoException ex)
        {
            _logger.LogInformation("SSO request rejected: {Error} - {Message}", ex.Error, ex.Message);
            return StatusCode(ex.StatusCode, new SsoErrorResponse { Error = ex.Error, ErrorDescription = ex.Message });
        }
    }
}
