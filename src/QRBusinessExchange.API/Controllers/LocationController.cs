using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.DTOs;
using QRBusinessExchange.API.Extensions;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Services;

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
    private readonly NearbyService _nearby;

    public LocationController(
        IUserRepository userRepository,
        IGeolocationService geolocationService,
        ILogger<LocationController> logger,
        NearbyService nearby)
    {
        _nearby = nearby;
        _userRepository = userRepository;
        _geolocationService = geolocationService;
        _logger = logger;
    }

    /// <summary>Your Ping Me setting and when your location was last shared.</summary>
    [HttpGet("ping-me")]
    [ProducesResponseType(typeof(PingMeStatusDto), StatusCodes.Status200OK)]
    public Task<IActionResult> GetPingMe() => WithUser(u => _nearby.GetStatusAsync(u));

    /// <summary>
    /// Turn Ping Me on or off. Off hides you from nearby maps straight away. On needs a shared
    /// location (409 location_required otherwise); POST update with pingMeEnabled=true does both.
    /// </summary>
    [HttpPut("ping-me")]
    [ProducesResponseType(typeof(PingMeStatusDto), StatusCodes.Status200OK)]
    public Task<IActionResult> SetPingMe([FromBody] SetPingMeRequest request) =>
        WithUser(u => _nearby.SetPingMeAsync(u, request.Enabled));

    /// <summary>Share your current position (and optionally turn Ping Me on or off).</summary>
    [HttpPost("update")]
    [ProducesResponseType(typeof(PingMeStatusDto), StatusCodes.Status200OK)]
    public Task<IActionResult> UpdateLocation([FromBody] ShareLocationRequest request) =>
        WithUser(u => _nearby.ShareLocationAsync(u, request.Latitude, request.Longitude, request.PingMeEnabled));

    /// <summary>People with Ping Me on near the given point, nearest first.</summary>
    [HttpGet("nearby")]
    [ProducesResponseType(typeof(List<NearbyUserDto>), StatusCodes.Status200OK)]
    public Task<IActionResult> GetNearbyUsers([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] double radiusMiles = 5) =>
        WithUser(u => _nearby.GetNearbyAsync(u, latitude, longitude, radiusMiles));

    private async Task<IActionResult> WithUser<T>(Func<Guid, Task<T>> action)
    {
        var userId = User.GetUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            return Ok(await action(userId.Value));
        }
        catch (SsoException ex)
        {
            return StatusCode(ex.StatusCode, new SsoErrorResponse { Error = ex.Error, ErrorDescription = ex.Message });
        }
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
