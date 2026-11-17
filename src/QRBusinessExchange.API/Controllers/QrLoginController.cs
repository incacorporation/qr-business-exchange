using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Interfaces;
using Microsoft.Extensions.Options;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// "Scan with your signed-in phone" QR code login.
/// </summary>
[ApiController]
[Route("api/qr-login")]
public class QrLoginController : ControllerBase
{
    private readonly QrLoginService _qrLoginService;
    private readonly WebAuthnService _webAuthnService;
    private readonly IUserRepository _userRepository;
    private readonly PortalSessionFactory _sessionFactory;
    private readonly SsoService _ssoService;
    private readonly SsoSettings _ssoSettings;
    private readonly ILogger<QrLoginController> _logger;

    public QrLoginController(
        QrLoginService qrLoginService,
        WebAuthnService webAuthnService,
        IUserRepository userRepository,
        PortalSessionFactory sessionFactory,
        SsoService ssoService,
        IOptions<SsoSettings> ssoSettings,
        ILogger<QrLoginController> logger)
    {
        _ssoService = ssoService;
        _ssoSettings = ssoSettings.Value;
        _qrLoginService = qrLoginService;
        _webAuthnService = webAuthnService;
        _userRepository = userRepository;
        _sessionFactory = sessionFactory;
        _logger = logger;
    }

    /// <summary>
    /// [Desktop] Start a QR login. Display <c>approveUrl</c> as a QR code and
    /// <c>verificationCode</c> next to it; keep <c>pollSecret</c> private.
    /// </summary>
    [HttpPost("sessions")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QrLoginCreateResponse), StatusCodes.Status200OK)]
    public Task<IActionResult> Create([FromBody] QrLoginCreateRequest? request) =>
        Run(() => _qrLoginService.CreateAsync(
            request?.ClientId,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            request?.RedirectUri));

    /// <summary>
    /// [Desktop, qrbound portal] Poll for the result. Returns a portal session exactly once after approval.
    /// For a request made for an application this only answers the portal itself; applications use
    /// <c>sessions/{id}/authorize</c>, which returns an authorization code instead.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/poll")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QrLoginPollResponse), StatusCodes.Status200OK)]
    public Task<IActionResult> Poll(Guid sessionId, [FromBody] QrLoginPollRequest request) =>
        Run(async () =>
        {
            var clientId = await _qrLoginService.GetClientIdForPollerAsync(sessionId, request.PollSecret);
            if (clientId != null && !_ssoSettings.IsPortalOrigin(Request.Headers.Origin.ToString()))
            {
                throw new SsoException("unauthorized_client",
                    "This sign-in request belongs to an application. Collect it with sessions/{id}/authorize.", 403);
            }

            var (status, expiresAt, approvedUserId) = await _qrLoginService.PollAsync(sessionId, request.PollSecret);
            var response = new QrLoginPollResponse { Status = status, ExpiresAt = expiresAt };

            if (approvedUserId.HasValue)
            {
                var user = await _userRepository.GetByIdAsync(approvedUserId.Value)
                    ?? throw new SsoException("access_denied", "User not found", 403);

                user.LastLoginAt = DateTime.UtcNow;
                await _userRepository.UpdateAsync(user);

                response.Auth = _sessionFactory.Create(user, "qr");
                _logger.LogInformation("QR login {SessionId} completed for {UserId}", sessionId, user.UserId);
            }

            return response;
        });

    /// <summary>
    /// [Application] Poll a QR sign-in that was created for <c>clientId</c>. Once the phone approves,
    /// returns a one-time authorization code bound to the client, redirect URI and PKCE challenge
    /// (exchange it at POST /api/sso/token). Never returns a portal session.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/authorize")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(QrLoginAuthorizeResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SsoErrorResponse), StatusCodes.Status400BadRequest)]
    public Task<IActionResult> AuthorizeForApplication(Guid sessionId, [FromBody] QrLoginAuthorizeRequest request) =>
        Run(async () =>
        {
            var sessionClientId = await _qrLoginService.GetClientIdForPollerAsync(sessionId, request.PollSecret);
            if (sessionClientId == null || !string.Equals(sessionClientId, request.ClientId, StringComparison.Ordinal))
            {
                throw new SsoException("invalid_client", "This sign-in request was not created for this application");
            }

            var authorizeRequest = new SsoAuthorizeRequest
            {
                ClientId = request.ClientId,
                RedirectUri = request.RedirectUri,
                CodeChallenge = request.CodeChallenge,
                CodeChallengeMethod = request.CodeChallengeMethod,
                Nonce = request.Nonce,
                State = request.State
            };

            // Check everything we can before the approval is consumed.
            await _ssoService.ValidateAuthorizeRequestAsync(authorizeRequest);

            var (status, expiresAt, approvedUserId) = await _qrLoginService.PollAsync(sessionId, request.PollSecret);
            var response = new QrLoginAuthorizeResponse { Status = status, ExpiresAt = expiresAt };

            if (approvedUserId.HasValue)
            {
                response.Code = await _ssoService.IssueAuthorizationCodeAsync(
                    approvedUserId.Value, "qr", DateTime.UtcNow, authorizeRequest);
                response.State = request.State;
                Response.Headers.CacheControl = "no-store";
                _logger.LogInformation("QR login {SessionId} issued a code to {ClientId} for {UserId}",
                    sessionId, request.ClientId, approvedUserId.Value);
            }

            return response;
        });

    /// <summary>
    /// [Phone] Details of the pending sign-in, shown before the user approves.
    /// </summary>
    [HttpGet("sessions/{sessionId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(QrLoginSessionInfoDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Get(Guid sessionId) =>
        WithUser(userId => _qrLoginService.GetForApproverAsync(sessionId, userId));

    /// <summary>
    /// [Phone] Get passkey options for approving this specific sign-in request.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/approve/init")]
    [Authorize]
    [ProducesResponseType(typeof(PasskeyAuthenticationOptionsDto), StatusCodes.Status200OK)]
    public Task<IActionResult> ApproveInit(Guid sessionId) =>
        WithUser(async userId =>
        {
            var user = await _userRepository.GetByIdAsync(userId)
                ?? throw new SsoException("access_denied", "User not found", 403);

            return await _webAuthnService.GenerateAuthenticationOptionsAsync(
                user.Email, QrLoginService.ApprovalPurpose(sessionId));
        });

    /// <summary>
    /// [Phone] Approve with the matching number and a passkey assertion.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/approve")]
    [Authorize]
    public Task<IActionResult> Approve(Guid sessionId, [FromBody] QrLoginApproveRequest request) =>
        WithUser(async userId =>
        {
            Guid passkeyUserId;
            try
            {
                var (success, credentialUserId) = await _webAuthnService.CompleteAuthenticationAsync(
                    request.Challenge, request.Assertion, QrLoginService.ApprovalPurpose(sessionId));

                if (!success || credentialUserId == null)
                {
                    throw new SsoException("passkey_failed", "Passkey verification failed", 401);
                }

                passkeyUserId = credentialUserId.Value;
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Passkey verification failed approving QR login {SessionId}", sessionId);
                throw new SsoException("passkey_failed", "Passkey verification failed", 401);
            }

            await _qrLoginService.ApproveAsync(sessionId, userId, passkeyUserId, request.VerificationCode);
            return new { status = "Approved" };
        });

    /// <summary>
    /// [Phone] Reject the sign-in request.
    /// </summary>
    [HttpPost("sessions/{sessionId:guid}/deny")]
    [Authorize]
    public Task<IActionResult> Deny(Guid sessionId) =>
        WithUser(async _ =>
        {
            await _qrLoginService.DenyAsync(sessionId);
            return new { status = "Denied" };
        });

    private Task<IActionResult> WithUser<T>(Func<Guid, Task<T>> action)
    {
        var userId = User.GetUserId();
        return userId == null
            ? Task.FromResult<IActionResult>(Unauthorized())
            : Run(() => action(userId.Value));
    }

    private async Task<IActionResult> Run<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (SsoException ex)
        {
            return StatusCode(ex.StatusCode, new SsoErrorResponse { Error = ex.Error, ErrorDescription = ex.Message });
        }
    }
}
