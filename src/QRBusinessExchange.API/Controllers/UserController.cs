using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.DTOs;
using QRBusinessExchange.Core.Interfaces;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for user profile management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UserController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ILogger<UserController> _logger;

    public UserController(IUserRepository userRepository, ILogger<UserController> logger)
    {
        _userRepository = userRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get current user profile
    /// </summary>
    /// <returns>User profile information</returns>
    [HttpGet("profile")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetProfile()
    {
        var userId = GetCurrentUserId();
        var user = await _userRepository.GetByIdAsync(userId);
        
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(MapToOwnProfile(user));
    }

    /// <summary>
    /// Update current user profile
    /// </summary>
    /// <param name="request">Updated profile information</param>
    /// <returns>Updated user profile</returns>
    [HttpPut("profile")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var userId = GetCurrentUserId();
        var user = await _userRepository.GetByIdAsync(userId);
        
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        // Update fields if provided
        if (!string.IsNullOrEmpty(request.FirstName))
            user.FirstName = request.FirstName;
        
        if (!string.IsNullOrEmpty(request.LastName))
            user.LastName = request.LastName;
        
        if (request.CompanyName != null)
            user.CompanyName = request.CompanyName;
        
        if (request.PhoneNumber != null)
            user.PhoneNumber = request.PhoneNumber;
        
        if (request.Bio != null)
            user.Bio = request.Bio;
        
        if (request.Skills != null)
            user.Skills = request.Skills;
        
        if (request.ServicePricing.HasValue)
            user.ServicePricing = request.ServicePricing;

        if (request.ProfileImageUrl != null)
        {
            var imageUrl = request.ProfileImageUrl.Trim();
            if (imageUrl.Length == 0)
            {
                user.ProfileImageUrl = null;
            }
            else if (imageUrl == user.ProfileImageUrl)
            {
                // Unchanged (e.g. an uploaded picture's /images/profiles/... path): keep it.
            }
            else if (imageUrl.Length > 500)
            {
                return BadRequest(new { error = "The profile image URL is too long (500 characters at most)." });
            }
            else if (Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp))
            {
                user.ProfileImageUrl = imageUrl;
            }
            else
            {
                return BadRequest(new { error = "The profile image URL must be a web address starting with https://" });
            }
        }

        user.UpdatedAt = DateTime.UtcNow;
        
        var updatedUser = await _userRepository.UpdateAsync(user);
        return Ok(MapToOwnProfile(updatedUser));
    }

    /// <summary>
    /// Get public profile of a user by ID
    /// </summary>
    /// <param name="id">User ID</param>
    /// <returns>Public user profile information</returns>
    [HttpGet("{id}/public")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublicProfile(Guid id)
    {
        var user = await _userRepository.GetByIdAsync(id);
        
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        return Ok(MapToUserDto(user));
    }

    /// <summary>
    /// Upload profile image
    /// </summary>
    /// <param name="file">Image file</param>
    /// <returns>Updated user profile with new image URL</returns>
    [HttpPost("profile-image")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadProfileImage(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "No file uploaded" });
        }

        if (file.Length > 5 * 1024 * 1024) // 5MB limit
        {
            return BadRequest(new { error = "File size exceeds 5MB limit" });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        
        if (!allowedExtensions.Contains(extension))
        {
            return BadRequest(new { error = "Invalid file type. Only JPG, PNG, and GIF are allowed" });
        }

        var userId = GetCurrentUserId();
        var user = await _userRepository.GetByIdAsync(userId);
        
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        // In a real implementation, upload to cloud storage (e.g., Azure Blob, AWS S3)
        // For now, just set a placeholder URL
        var fileName = $"{userId}_{DateTime.UtcNow.Ticks}{extension}";
        user.ProfileImageUrl = $"/images/profiles/{fileName}";
        user.UpdatedAt = DateTime.UtcNow;

        var updatedUser = await _userRepository.UpdateAsync(user);
        return Ok(MapToOwnProfile(updatedUser));
    }

    /// <summary>
    /// Update user location
    /// </summary>
    /// <param name="request">Location coordinates</param>
    /// <returns>Success message</returns>
    [HttpPut("location")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationRequest request)
    {
        var userId = GetCurrentUserId();
        
        // In a real implementation, update UserLocation table
        // For now, just return success
        return Ok(new { message = "Location updated successfully" });
    }

    /// <summary>
    /// Update ping me status
    /// </summary>
    /// <param name="enabled">Enable or disable ping me</param>
    /// <returns>Success message</returns>
    [HttpPut("ping-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePingStatus([FromQuery] bool enabled)
    {
        var userId = GetCurrentUserId();
        
        // In a real implementation, update UserLocation.PingMeEnabled
        // For now, just return success
        return Ok(new { message = $"Ping Me {(enabled ? "enabled" : "disabled")} successfully" });
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = HttpContext.Items["UserId"];
        if (userIdClaim != null && userIdClaim is Guid userId)
        {
            return userId;
        }

        // Fallback to claims if middleware didn't set it
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) 
                    ?? User.FindFirst("sub");
        
        if (claim != null && Guid.TryParse(claim.Value, out var parsedUserId))
        {
            return parsedUserId;
        }

        throw new UnauthorizedAccessException("User ID not found in token");
    }

    /// <summary>The signed-in user's own profile: the public fields plus the role (not shown on public profiles).</summary>
    private static UserDto MapToOwnProfile(QRBusinessExchange.Core.Entities.User user)
    {
        var dto = MapToUserDto(user);
        dto.Role = user.Role;
        return dto;
    }

    private static UserDto MapToUserDto(QRBusinessExchange.Core.Entities.User user)
    {
        return new UserDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            CompanyName = user.CompanyName,
            PhoneNumber = user.PhoneNumber,
            ProfileImageUrl = user.ProfileImageUrl,
            Bio = user.Bio,
            Skills = user.Skills,
            ServicePricing = user.ServicePricing,
            IsEmailVerified = user.IsEmailVerified,
            CreatedAt = user.CreatedAt
        };
    }
}
