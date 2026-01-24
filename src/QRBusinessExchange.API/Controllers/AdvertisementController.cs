using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.DTOs;
using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for advertisement management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AdvertisementController : ControllerBase
{
    private readonly ILogger<AdvertisementController> _logger;

    public AdvertisementController(ILogger<AdvertisementController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Create a new advertisement
    /// </summary>
    /// <param name="request">Advertisement details</param>
    /// <returns>Created advertisement</returns>
    [HttpPost("create")]
    [ProducesResponseType(typeof(AdvertisementDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateAdvertisement([FromBody] AdvertisementRequest request)
    {
        var userId = GetCurrentUserId();

        if (request.StartDate >= request.EndDate)
        {
            return BadRequest(new { error = "End date must be after start date" });
        }

        if (request.StartDate < DateTime.UtcNow.Date)
        {
            return BadRequest(new { error = "Start date cannot be in the past" });
        }

        var days = (request.EndDate - request.StartDate).Days;
        var costPerDay = 5.00m;
        var totalCost = days * costPerDay;

        // In a real implementation, save to Advertisement table
        var advertisement = new Advertisement
        {
            AdId = Guid.NewGuid(),
            UserId = userId,
            AdContent = request.AdContent,
            ImageUrl = request.ImageUrl,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            CostPerDay = costPerDay,
            TotalCost = totalCost,
            IsActive = true,
            Impressions = 0,
            Clicks = 0,
            CreatedAt = DateTime.UtcNow
        };

        return CreatedAtAction(nameof(CreateAdvertisement), MapToAdvertisementDto(advertisement));
    }

    /// <summary>
    /// Get all active advertisements
    /// </summary>
    /// <returns>List of active advertisements</returns>
    [HttpGet("active")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<AdvertisementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActiveAdvertisements()
    {
        // In a real implementation, query Advertisement table
        var advertisements = new List<AdvertisementDto>();
        
        return Ok(advertisements);
    }

    /// <summary>
    /// Get current user's advertisements
    /// </summary>
    /// <returns>List of user's advertisements</returns>
    [HttpGet("my-ads")]
    [ProducesResponseType(typeof(IEnumerable<AdvertisementDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyAdvertisements()
    {
        var userId = GetCurrentUserId();
        
        // In a real implementation, query Advertisement table filtered by userId
        var advertisements = new List<AdvertisementDto>();
        
        return Ok(advertisements);
    }

    /// <summary>
    /// Update an advertisement
    /// </summary>
    /// <param name="id">Advertisement ID</param>
    /// <param name="request">Updated advertisement details</param>
    /// <returns>Updated advertisement</returns>
    [HttpPut("{id}")]
    [ProducesResponseType(typeof(AdvertisementDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateAdvertisement(Guid id, [FromBody] AdvertisementRequest request)
    {
        var userId = GetCurrentUserId();
        
        // In a real implementation, find and update advertisement
        return NotFound(new { error = "Advertisement not found" });
    }

    /// <summary>
    /// Delete an advertisement
    /// </summary>
    /// <param name="id">Advertisement ID</param>
    /// <returns>Success message</returns>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteAdvertisement(Guid id)
    {
        var userId = GetCurrentUserId();
        
        // In a real implementation, find and delete advertisement
        return Ok(new { message = "Advertisement deleted successfully" });
    }

    /// <summary>
    /// Track advertisement impression
    /// </summary>
    /// <param name="id">Advertisement ID</param>
    /// <returns>Success message</returns>
    [HttpPost("{id}/track-impression")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TrackImpression(Guid id)
    {
        // In a real implementation, increment impressions count
        return Ok(new { message = "Impression tracked" });
    }

    /// <summary>
    /// Track advertisement click
    /// </summary>
    /// <param name="id">Advertisement ID</param>
    /// <returns>Success message</returns>
    [HttpPost("{id}/track-click")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> TrackClick(Guid id)
    {
        // In a real implementation, increment clicks count
        return Ok(new { message = "Click tracked" });
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

    private static AdvertisementDto MapToAdvertisementDto(Advertisement ad)
    {
        return new AdvertisementDto
        {
            AdId = ad.AdId,
            AdContent = ad.AdContent,
            ImageUrl = ad.ImageUrl,
            StartDate = ad.StartDate,
            EndDate = ad.EndDate,
            CostPerDay = ad.CostPerDay,
            TotalCost = ad.TotalCost,
            IsActive = ad.IsActive,
            Impressions = ad.Impressions,
            Clicks = ad.Clicks,
            CreatedAt = ad.CreatedAt
        };
    }
}
