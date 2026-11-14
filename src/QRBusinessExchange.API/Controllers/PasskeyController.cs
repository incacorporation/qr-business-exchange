using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for WebAuthn/FIDO2 passkey authentication
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class PasskeyController : ControllerBase
{
    private readonly WebAuthnService _webAuthnService;
    private readonly IUserRepository _userRepository;
    private readonly IPasskeyRepository _passkeyRepository;
    private readonly PortalSessionFactory _sessionFactory;
    private readonly ILogger<PasskeyController> _logger;

    public PasskeyController(
        WebAuthnService webAuthnService,
        IUserRepository userRepository,
        IPasskeyRepository passkeyRepository,
        PortalSessionFactory sessionFactory,
        ILogger<PasskeyController> logger)
    {
        _webAuthnService = webAuthnService;
        _userRepository = userRepository;
        _passkeyRepository = passkeyRepository;
        _sessionFactory = sessionFactory;
        _logger = logger;
    }

    /// <summary>
    /// Initiate passkey registration for the signed-in account.
    /// </summary>
    /// <remarks>The account is taken from the access token; any email in the body is ignored.</remarks>
    [HttpPost("register/init")]
    [Authorize]
    [ProducesResponseType(typeof(PasskeyRegistrationOptionsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InitiateRegistration([FromBody] PasskeyRegistrationInitRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        try
        {
            var options = await _webAuthnService.GenerateRegistrationOptionsAsync(user.Email, request.DeviceName);
            return Ok(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating passkey registration for {UserId}", user.UserId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Complete passkey registration - Verify and store credential
    /// </summary>
    [HttpPost("register/complete")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CompleteRegistration([FromBody] PasskeyRegistrationCompleteRequest request)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        try
        {
            var success = await _webAuthnService.CompleteRegistrationAsync(
                user.Email,
                request.Challenge,
                request.Credential,
                request.DeviceName);

            return success
                ? Ok(new { message = "Passkey registered successfully" })
                : BadRequest(new { error = "Failed to register passkey" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing passkey registration for {UserId}", user.UserId);
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Initiate passkey authentication - Get authentication options
    /// </summary>
    [HttpPost("authenticate/init")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PasskeyAuthenticationOptionsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> InitiateAuthentication([FromBody] PasskeyAuthenticationInitRequest request)
    {
        try
        {
            var options = await _webAuthnService.GenerateAuthenticationOptionsAsync(request.Email);
            return Ok(options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initiating passkey authentication");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Complete passkey authentication - Verify assertion and return a portal session
    /// </summary>
    [HttpPost("authenticate/complete")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CompleteAuthentication([FromBody] PasskeyAuthenticationCompleteRequest request)
    {
        try
        {
            var (success, userId) = await _webAuthnService.CompleteAuthenticationAsync(
                request.Challenge,
                request.Assertion);

            if (!success || !userId.HasValue)
            {
                return Unauthorized(new { error = "Authentication failed" });
            }

            var user = await _userRepository.GetByIdAsync(userId.Value);
            if (user == null)
            {
                return Unauthorized(new { error = "User not found" });
            }

            if (!user.IsActive)
            {
                return Unauthorized(new { error = "Account is deactivated" });
            }

            user.LastLoginAt = DateTime.UtcNow;
            await _userRepository.UpdateAsync(user);

            return Ok(_sessionFactory.Create(user, "passkey"));
        }
        catch (PasskeyNotRegisteredException)
        {
            _logger.LogInformation("Passkey sign-in with a credential this server doesn't know");
            return Unauthorized(new
            {
                error = "passkey_not_registered",
                error_description = "This passkey isn't registered with qrbound. Log in with your password, " +
                                    "then add a new passkey in Settings."
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Passkey authentication failed");
            return Unauthorized(new { error = "Passkey authentication failed" });
        }
    }

    /// <summary>
    /// Get list of registered passkeys for the current user
    /// </summary>
    [HttpGet("list")]
    [Authorize]
    [ProducesResponseType(typeof(PasskeyListDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPasskeys()
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        var credentials = await _passkeyRepository.GetByUserIdAsync(user.UserId);

        var passkeys = credentials.Select(c => new PasskeyInfoDto
        {
            CredentialId = Convert.ToBase64String(c.CredentialId),
            DeviceName = c.DeviceName,
            CreatedAt = c.CreatedAt,
            LastUsed = c.LastUsed
        }).ToList();

        return Ok(new PasskeyListDto { Passkeys = passkeys });
    }

    /// <summary>
    /// Delete a registered passkey
    /// </summary>
    [HttpDelete("{credentialId}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeletePasskey(string credentialId)
    {
        var user = await GetCurrentUserAsync();
        if (user == null) return Unauthorized();

        byte[] credentialIdBytes;
        try
        {
            credentialIdBytes = Convert.FromBase64String(credentialId);
        }
        catch (FormatException)
        {
            return BadRequest(new { error = "Invalid credential id" });
        }

        var credential = await _passkeyRepository.GetByCredentialIdAsync(credentialIdBytes);
        if (credential == null || credential.UserId != user.UserId)
        {
            return NotFound(new { error = "Passkey not found" });
        }

        await _passkeyRepository.DeleteAsync(credentialIdBytes);
        return Ok(new { message = "Passkey deleted successfully" });
    }

    /// <summary>
    /// Check if user has any registered passkeys
    /// </summary>
    [HttpGet("has-passkeys")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> HasPasskeys([FromQuery] string email)
    {
        try
        {
            var user = await _userRepository.GetByEmailAsync(email);
            if (user == null)
            {
                return Ok(new { hasPasskeys = false });
            }

            var hasPasskeys = await _passkeyRepository.UserHasPasskeysAsync(user.UserId);
            return Ok(new { hasPasskeys });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking passkeys for {Email}", email);
            return Ok(new { hasPasskeys = false });
        }
    }

    private async Task<Core.Entities.User?> GetCurrentUserAsync()
    {
        var userId = User.GetUserId();
        return userId.HasValue ? await _userRepository.GetByIdAsync(userId.Value) : null;
    }
}
