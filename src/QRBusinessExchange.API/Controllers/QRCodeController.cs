using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.Core.Interfaces;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for QR code generation and tracking
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class QRCodeController : ControllerBase
{
    private readonly IQRCodeService _qrCodeService;
    private readonly IUserRepository _userRepository;
    private readonly ILogger<QRCodeController> _logger;

    public QRCodeController(
        IQRCodeService qrCodeService,
        IUserRepository userRepository,
        ILogger<QRCodeController> logger)
    {
        _qrCodeService = qrCodeService;
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Generate QR code for current user
    /// </summary>
    /// <returns>QR code image as base64 string</returns>
    [HttpGet("generate")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateQRCode()
    {
        var userId = GetCurrentUserId();
        var user = await _userRepository.GetByIdAsync(userId);
        
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        // Generate profile URL
        var profileUrl = $"{Request.Scheme}://{Request.Host}/api/qrcode/profile/{userId}";

        // Generate QR code
        var qrCode = await _qrCodeService.GenerateQRCodeAsync(userId);
        var qrCodeData = qrCode.QRCodeData;

        return Ok(new
        {
            qrCodeData,
            profileUrl,
            userId = user.UserId,
            name = $"{user.FirstName} {user.LastName}",
            company = user.CompanyName
        });
    }

    /// <summary>
    /// Get user profile by scanning QR code
    /// </summary>
    /// <param name="userId">User ID from QR code</param>
    /// <returns>User public profile</returns>
    [HttpGet("profile/{userId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetProfileByQRCode(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(new
        {
            userId = user.UserId,
            firstName = user.FirstName,
            lastName = user.LastName,
            companyName = user.CompanyName,
            phoneNumber = user.PhoneNumber,
            email = user.Email,
            profileImageUrl = user.ProfileImageUrl,
            bio = user.Bio,
            skills = user.Skills,
            servicePricing = user.ServicePricing
        });
    }

    /// <summary>
    /// Track QR code scan
    /// </summary>
    /// <param name="userId">User ID whose QR code was scanned</param>
    /// <returns>Success message</returns>
    [HttpPost("track-scan/{userId}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TrackScan(Guid userId)
    {
        // In a real implementation, increment QRCode.ScansCount
        // and update UsageAnalytics.QRScans
        
        _logger.LogInformation("QR code scanned for user {UserId}", userId);
        
        return Ok(new { message = "Scan tracked successfully" });
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = HttpContext.Items["UserId"];
        if (userIdClaim != null && userIdClaim is Guid userId)
        {
            return userId;
        }

        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) 
                    ?? User.FindFirst("sub");
        
        if (claim != null && Guid.TryParse(claim.Value, out var parsedUserId))
        {
            return parsedUserId;
        }

        throw new UnauthorizedAccessException("User ID not found in token");
    }
}
