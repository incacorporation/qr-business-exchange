using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.DTOs;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Services;

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
    private readonly ProfileImageService _images;

    public UserController(IUserRepository userRepository, ILogger<UserController> logger, ProfileImageService images)
    {
        _userRepository = userRepository;
        _images = images;
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

    /// <summary>The signed-in user's text-message consent (the Settings checkbox).</summary>
    [HttpGet("sms-consent")]
    [ProducesResponseType(typeof(Core.DTOs.SmsConsentDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSmsConsent()
    {
        var user = await _userRepository.GetByIdAsync(GetCurrentUserId());
        return user == null ? NotFound(new { error = "User not found" }) : Ok(ToSmsConsent(user));
    }

    /// <summary>
    /// Opts in to, or out of, account and security text messages. Opting in records the number, time and IP
    /// address as proof of consent and needs a phone number on the profile; opting out keeps that record but
    /// stops all texts.
    /// </summary>
    [HttpPut("sms-consent")]
    [ProducesResponseType(typeof(Core.DTOs.SmsConsentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SetSmsConsent([FromBody] Core.DTOs.SetSmsConsentRequest request)
    {
        var user = await _userRepository.GetByIdAsync(GetCurrentUserId());
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        if (request.OptIn)
        {
            var phone = user.PhoneNumber?.Trim();
            if (string.IsNullOrEmpty(phone))
            {
                return BadRequest(new { error = "Add your mobile number to your profile first, then turn on text messages." });
            }

            user.SmsOptIn = true;
            user.SmsOptInAt = DateTime.UtcNow;
            user.SmsOptInPhone = phone;
            user.SmsOptInIp = HttpContext.Connection.RemoteIpAddress?.ToString();
        }
        else
        {
            user.SmsOptIn = false;
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        return Ok(ToSmsConsent(user));
    }

    private static Core.DTOs.SmsConsentDto ToSmsConsent(Core.Entities.User user) => new()
    {
        OptIn = user.SmsOptIn,
        OptInAt = user.SmsOptIn ? user.SmsOptInAt : null,
        PhoneNumber = string.IsNullOrWhiteSpace(user.PhoneNumber) ? null : user.PhoneNumber
    };

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
        {
            user.PhoneNumber = request.PhoneNumber;

            // Text-message consent was given for one number; a new number hasn't agreed to texts.
            if (user.SmsOptIn && !string.Equals(user.PhoneNumber.Trim(), user.SmsOptInPhone, StringComparison.Ordinal))
            {
                user.SmsOptIn = false;
            }
        }
        
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

        if (request.Visibility is { } show)
        {
            user.ShowEmail = show.ShowEmail ?? user.ShowEmail;
            user.ShowCompanyName = show.ShowCompanyName ?? user.ShowCompanyName;
            user.ShowPhoneNumber = show.ShowPhoneNumber ?? user.ShowPhoneNumber;
            user.ShowBio = show.ShowBio ?? user.ShowBio;
            user.ShowSkills = show.ShowSkills ?? user.ShowSkills;
            user.ShowServicePricing = show.ShowServicePricing ?? user.ShowServicePricing;
            user.ShowProfileImage = show.ShowProfileImage ?? user.ShowProfileImage;
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

        return Ok(MapToPublicProfile(user));
    }

    /// <summary>
    /// Upload a profile picture (personal photo or business logo): JPG, JPEG, PNG or GIF, up to 5 MB.
    /// The file's bytes decide its type, not its name. Replaces any earlier picture.
    /// </summary>
    [HttpPost("profile-image")]
    [RequestSizeLimit(ProfileImageService.MaxBytes + 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = ProfileImageService.MaxBytes + 1024 * 1024)]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UploadProfileImage(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { error = "Choose a picture to upload." });
        }
        if (file.Length > ProfileImageService.MaxBytes)
        {
            return BadRequest(new { error = "The picture is larger than 5 MB. Choose a smaller one." });
        }

        var userId = GetCurrentUserId();
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);

        try
        {
            await _images.SaveAsync(userId, buffer.ToArray(), version => ImageUrl(userId, version));
        }
        catch (SsoException ex)
        {
            return StatusCode(ex.StatusCode, new { error = ex.Message });
        }

        var user = await _userRepository.GetByIdAsync(userId);
        return user == null ? NotFound(new { error = "User not found" }) : Ok(MapToOwnProfile(user));
    }

    /// <summary>Remove your profile picture.</summary>
    [HttpDelete("profile-image")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> RemoveProfileImage()
    {
        var userId = GetCurrentUserId();
        await _images.RemoveAsync(userId);
        var user = await _userRepository.GetByIdAsync(userId);
        return user == null ? NotFound(new { error = "User not found" }) : Ok(MapToOwnProfile(user));
    }

    /// <summary>Your own picture, even when it's hidden from your card (for the Edit Profile preview).</summary>
    [HttpGet("profile-image")]
    public async Task<IActionResult> GetOwnProfileImage()
    {
        var image = await _images.GetOwnAsync(GetCurrentUserId());
        if (image is not { } found)
        {
            return NotFound();
        }

        Response.Headers.CacheControl = "private, no-cache";
        return ImageFile(found.ContentType, found.Data);
    }

    /// <summary>
    /// Someone's picture, as linked from their business card and contact entries. Not found when they
    /// have none or untick "Show on my card".
    /// </summary>
    [HttpGet("{id:guid}/image")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProfileImage(Guid id)
    {
        var image = await _images.GetPublicAsync(id);
        if (image is not { } found)
        {
            return NotFound();
        }

        // Addresses carry a version (?v=), so a new upload is a new address; a day keeps hiding it timely.
        Response.Headers.CacheControl = "public, max-age=86400";
        return ImageFile(found.ContentType, found.Data);
    }

    private FileContentResult ImageFile(string contentType, byte[] data)
    {
        // Never let a browser treat the bytes as anything but the image type checked on upload.
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return File(data, contentType);
    }

    /// <summary>Absolute address of the user's picture on this API (the web app runs on another host).</summary>
    private string ImageUrl(Guid userId, DateTime version) =>
        $"{Request.Scheme}://{Request.Host}{Request.PathBase}/api/user/{userId}/image?v={version.Ticks}";


    // Location and Ping Me live in LocationController (api/location/...).

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
        dto.Visibility = new ProfileVisibilityDto
        {
            ShowEmail = user.ShowEmail,
            ShowCompanyName = user.ShowCompanyName,
            ShowPhoneNumber = user.ShowPhoneNumber,
            ShowBio = user.ShowBio,
            ShowSkills = user.ShowSkills,
            ShowServicePricing = user.ShowServicePricing,
            ShowProfileImage = user.ShowProfileImage
        };
        return dto;
    }

    /// <summary>What anyone opening the user's business card sees: the name plus the fields they chose to show.</summary>
    private static UserDto MapToPublicProfile(QRBusinessExchange.Core.Entities.User user)
    {
        var dto = MapToUserDto(user);
        if (!user.ShowEmail) dto.Email = string.Empty;
        if (!user.ShowCompanyName) dto.CompanyName = null;
        if (!user.ShowPhoneNumber) dto.PhoneNumber = null;
        if (!user.ShowBio) dto.Bio = null;
        if (!user.ShowSkills) dto.Skills = null;
        if (!user.ShowServicePricing) dto.ServicePricing = null;
        if (!user.ShowProfileImage) dto.ProfileImageUrl = null;
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
