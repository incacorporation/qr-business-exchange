using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.Core.Interfaces;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for analytics and usage statistics
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AnalyticsController : ControllerBase
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly IConnectionRepository _connectionRepository;
    private readonly ILogger<AnalyticsController> _logger;

    public AnalyticsController(
        ISubscriptionRepository subscriptionRepository,
        IConnectionRepository connectionRepository,
        ILogger<AnalyticsController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _connectionRepository = connectionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get analytics dashboard
    /// </summary>
    /// <returns>Dashboard statistics</returns>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDashboard()
    {
        var userId = GetCurrentUserId();
        
        // Get subscription info
        var subscription = await _subscriptionRepository.GetActiveSubscriptionAsync(userId);
        
        // Get connections
        var connections = await _connectionRepository.GetHistoryAsync(userId);
        var pendingConnections = await _connectionRepository.GetPendingByUserIdAsync(userId);
        
        return Ok(new
        {
            subscription = new
            {
                type = subscription?.SubscriptionType ?? "Free",
                creditsAvailable = subscription?.CreditsAvailable ?? 0,
                creditsUsed = subscription?.CreditsUsed ?? 0
            },
            connections = new
            {
                total = connections.Count(),
                pending = pendingConnections.Count(),
                accepted = connections.Count(c => c.Status == "Accepted"),
                rejected = connections.Count(c => c.Status == "Rejected")
            },
            activity = new
            {
                qrScans = 0,
                profileViews = 0,
                mapViews = 0
            }
        });
    }

    /// <summary>
    /// Get detailed usage statistics
    /// </summary>
    /// <param name="startDate">Start date for statistics</param>
    /// <param name="endDate">End date for statistics</param>
    /// <returns>Usage statistics</returns>
    [HttpGet("usage")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsage([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        var userId = GetCurrentUserId();
        
        var start = startDate ?? DateTime.UtcNow.AddMonths(-1);
        var end = endDate ?? DateTime.UtcNow;
        
        // In a real implementation, query UsageAnalytics table
        return Ok(new
        {
            period = new { start, end },
            usage = new
            {
                connectionsMade = 0,
                qrScans = 0,
                profileViews = 0,
                mapViews = 0
            },
            trends = new
            {
                dailyAverage = 0,
                weeklyAverage = 0,
                monthlyAverage = 0
            }
        });
    }

    /// <summary>
    /// Get credit usage breakdown
    /// </summary>
    /// <returns>Credit usage details</returns>
    [HttpGet("credits")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCreditUsage()
    {
        var userId = GetCurrentUserId();
        var subscription = await _subscriptionRepository.GetActiveSubscriptionAsync(userId);
        
        if (subscription == null)
        {
            return Ok(new
            {
                available = 0,
                used = 0,
                total = 0,
                breakdown = new object[] { }
            });
        }

        return Ok(new
        {
            available = subscription.CreditsAvailable,
            used = subscription.CreditsUsed,
            total = subscription.CreditsAvailable + subscription.CreditsUsed,
            breakdown = new[]
            {
                new { category = "Connections", count = subscription.CreditsUsed },
                new { category = "Remaining", count = subscription.CreditsAvailable }
            },
            subscription = new
            {
                type = subscription.SubscriptionType,
                startDate = subscription.StartDate,
                endDate = subscription.EndDate
            }
        });
    }

    /// <summary>
    /// Get activity timeline
    /// </summary>
    /// <param name="days">Number of days to include</param>
    /// <returns>Activity timeline</returns>
    [HttpGet("timeline")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActivityTimeline([FromQuery] int days = 30)
    {
        var userId = GetCurrentUserId();
        
        if (days <= 0 || days > 365)
        {
            return BadRequest(new { error = "Days must be between 1 and 365" });
        }
        
        var startDate = DateTime.UtcNow.AddDays(-days);
        var timeline = new List<object>();
        
        // In a real implementation, aggregate data from UsageAnalytics table
        for (int i = 0; i < days; i++)
        {
            var date = startDate.AddDays(i);
            timeline.Add(new
            {
                date = date.Date,
                connections = 0,
                qrScans = 0,
                profileViews = 0
            });
        }
        
        return Ok(timeline);
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
