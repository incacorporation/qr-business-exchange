using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.DTOs;
using QRBusinessExchange.Core.Interfaces;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for location-based operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LocationController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly IGeolocationService _geolocationService;
    private readonly ILogger<LocationController> _logger;

    public LocationController(
        IUserRepository userRepository,
        IGeolocationService geolocationService,
        ILogger<LocationController> logger)
    {
        _userRepository = userRepository;
        _geolocationService = geolocationService;
        _logger = logger;
    }

    /// <summary>
    /// Get nearby users within specified radius
    /// </summary>
    /// <param name="request">Location and radius parameters</param>
    /// <returns>List of nearby users</returns>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(IEnumerable<UserLocationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetNearbyUsers([FromQuery] decimal latitude, [FromQuery] decimal longitude, [FromQuery] int radiusYards = 1000)
    {
        if (radiusYards <= 0 || radiusYards > 50000)
        {
            return BadRequest(new { error = "Radius must be between 1 and 50000 yards" });
        }

        // In a real implementation, query UserLocation table with spatial queries
        // For now, return an empty list
        var nearbyUsers = new List<UserLocationDto>();

        return Ok(nearbyUsers);
    }

    /// <summary>
    /// Update current user's location
    /// </summary>
    /// <param name="request">Location coordinates</param>
    /// <returns>Success message</returns>
    [HttpPost("update")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpdateLocation([FromBody] UpdateLocationRequest request)
    {
        var userId = GetCurrentUserId();

        if (request.Latitude < -90 || request.Latitude > 90)
        {
            return BadRequest(new { error = "Latitude must be between -90 and 90" });
        }

        if (request.Longitude < -180 || request.Longitude > 180)
        {
            return BadRequest(new { error = "Longitude must be between -180 and 180" });
        }

        // In a real implementation, update UserLocation table
        // For now, just return success
        return Ok(new { message = "Location updated successfully" });
    }

    /// <summary>
    /// Calculate distance between two locations
    /// </summary>
    /// <param name="lat1">First location latitude</param>
    /// <param name="lon1">First location longitude</param>
    /// <param name="lat2">Second location latitude</param>
    /// <param name="lon2">Second location longitude</param>
    /// <returns>Distance in yards</returns>
    [HttpGet("distance")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult CalculateDistance(
        [FromQuery] decimal lat1, 
        [FromQuery] decimal lon1, 
        [FromQuery] decimal lat2, 
        [FromQuery] decimal lon2)
    {
        var distanceYards = _geolocationService.CalculateDistance(lat1, lon1, lat2, lon2);

        return Ok(new
        {
            distanceYards,
            distanceFeet = distanceYards * 3,
            distanceMiles = distanceYards / 1760.0m
        });
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
