using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// "Link a phone": register a passkey on a phone by scanning a QR code shown on a signed-in computer.
/// Afterwards the phone is signed in and can approve "scan with your phone" sign-ins.
/// </summary>
[ApiController]
[Route("api/device-link")]
public class DeviceLinkController : ControllerBase
{
    private readonly DeviceLinkService _links;
    private readonly WebAuthnService _webAuthn;
    private readonly IUserRepository _users;
    private readonly PortalSessionFactory _sessionFactory;
    private readonly ILogger<DeviceLinkController> _logger;

    public DeviceLinkController(
        DeviceLinkService links,
        WebAuthnService webAuthn,
        IUserRepository users,
        PortalSessionFactory sessionFactory,
        ILogger<DeviceLinkController> logger)
    {
        _links = links;
        _webAuthn = webAuthn;
        _users = users;
        _sessionFactory = sessionFactory;
        _logger = logger;
    }

    // ---------------------------------------------------------------- computer

    /// <summary>[Computer] Start linking a phone. Show <c>linkUrl</c> as a QR code.</summary>
    [HttpPost("sessions")]
    [Authorize]
    [ProducesResponseType(typeof(DeviceLinkCreateResponse), StatusCodes.Status200OK)]
    public Task<IActionResult> Create() =>
        WithUser(async userId =>
        {
            var user = await _users.GetByIdAsync(userId);
            if (user == null || !user.IsActive)
            {
                throw new SsoException("access_denied", "Account not found or deactivated", 403);
            }

            return _links.Create(user.UserId, user.Email).Response;
        });

    /// <summary>[Computer] Poll the link; once a phone scanned, includes the numbers to choose from.</summary>
    [HttpGet("sessions/{sessionId:guid}")]
    [Authorize]
    [ProducesResponseType(typeof(DeviceLinkStatusDto), StatusCodes.Status200OK)]
    public Task<IActionResult> Get(Guid sessionId) =>
        WithUser(userId => Task.FromResult(_links.GetForOwner(sessionId, userId)));

    /// <summary>[Computer] Confirm the number displayed on the phone.</summary>
    [HttpPost("sessions/{sessionId:guid}/confirm")]
    [Authorize]
    public Task<IActionResult> Confirm(Guid sessionId, [FromBody] DeviceLinkConfirmRequest request) =>
        WithUser(userId =>
        {
            _links.Confirm(sessionId, userId, request.VerificationCode);
            return Task.FromResult<object>(new { status = DeviceLinkStatus.Confirmed });
        });

    /// <summary>[Computer] Cancel the link.</summary>
    [HttpPost("sessions/{sessionId:guid}/cancel")]
    [Authorize]
    public Task<IActionResult> Cancel(Guid sessionId) =>
        WithUser(userId =>
        {
            _links.Cancel(sessionId, userId);
            return Task.FromResult<object>(new { status = DeviceLinkStatus.Denied });
        });

    // ------------------------------------------------------------------- phone

    /// <summary>[Phone] Claim the link with the secret from the QR code. The first phone wins.</summary>
    [HttpPost("sessions/{sessionId:guid}/claim")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DeviceLinkClaimResponse), StatusCodes.Status200OK)]
    public Task<IActionResult> Claim(Guid sessionId, [FromBody] DeviceLinkClaimRequest request) =>
        Run(() => Task.FromResult(_links.Claim(
            sessionId,
            request.LinkSecret,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString())));

    /// <summary>[Phone] Poll until the computer confirms the number.</summary>
    [HttpPost("sessions/{sessionId:guid}/status")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DeviceLinkPhoneStatusDto), StatusCodes.Status200OK)]
    public Task<IActionResult> PhoneStatus(Guid sessionId, [FromBody] DeviceLinkPhoneRequest request) =>
        Run(() => Task.FromResult(_links.GetForPhone(sessionId, request.PhoneSecret)));

    /// <summary>[Phone] Cancel from the phone.</summary>
    [HttpPost("sessions/{sessionId:guid}/phone-cancel")]
    [AllowAnonymous]
    public Task<IActionResult> PhoneCancel(Guid sessionId, [FromBody] DeviceLinkPhoneRequest request) =>
        Run(() =>
        {
            _links.CancelFromPhone(sessionId, request.PhoneSecret);
            return Task.FromResult<object>(new { status = DeviceLinkStatus.Denied });
        });

    /// <summary>[Phone] Passkey creation options (only after the number was confirmed).</summary>
    [HttpPost("sessions/{sessionId:guid}/register/init")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasskeyRegistrationOptionsDto), StatusCodes.Status200OK)]
    public Task<IActionResult> RegisterInit(Guid sessionId, [FromBody] DeviceLinkRegisterInitRequest request) =>
        Run(async () =>
        {
            var (_, email) = _links.RequireConfirmed(sessionId, request.PhoneSecret);
            return await _webAuthn.GenerateRegistrationOptionsAsync(
                email, request.DeviceName, DeviceLinkService.RegistrationPurpose(sessionId));
        });

    /// <summary>[Phone] Save the phone's passkey and sign the phone in.</summary>
    [HttpPost("sessions/{sessionId:guid}/register/complete")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    public Task<IActionResult> RegisterComplete(Guid sessionId, [FromBody] DeviceLinkRegisterCompleteRequest request) =>
        Run(async () =>
        {
            var (userId, email) = _links.RequireConfirmed(sessionId, request.PhoneSecret);
            var deviceName = string.IsNullOrWhiteSpace(request.DeviceName) ? "Linked phone" : request.DeviceName.Trim();
            if (deviceName.Length > 100) deviceName = deviceName[..100];

            try
            {
                await _webAuthn.CompleteRegistrationAsync(
                    email, request.Challenge, request.Credential, deviceName,
                    DeviceLinkService.RegistrationPurpose(sessionId));
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Passkey registration failed for device link {SessionId}", sessionId);
                throw new SsoException("passkey_failed", "The passkey could not be saved. Try again.", 400);
            }

            _links.Complete(sessionId, request.PhoneSecret, deviceName);

            var user = await _users.GetByIdAsync(userId)
                ?? throw new SsoException("access_denied", "User not found", 403);
            user.LastLoginAt = DateTime.UtcNow;
            await _users.UpdateAsync(user);

            _logger.LogInformation("Phone linked to {UserId} via device link {SessionId}", userId, sessionId);

            // The phone just proved possession of a brand-new passkey for this account.
            return _sessionFactory.Create(user, "passkey");
        });

    // ----------------------------------------------------------------- helpers

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
