using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.DTOs;
using QRBusinessExchange.Core.Interfaces;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for subscription and credit management
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly ISubscriptionRepository _subscriptionRepository;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(
        ISubscriptionRepository subscriptionRepository,
        ILogger<SubscriptionController> logger)
    {
        _subscriptionRepository = subscriptionRepository;
        _logger = logger;
    }

    /// <summary>
    /// Get current active subscription
    /// </summary>
    /// <returns>Current subscription details</returns>
    [HttpGet("current")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentSubscription()
    {
        var userId = GetCurrentUserId();
        var subscription = await _subscriptionRepository.GetActiveSubscriptionAsync(userId);
        
        if (subscription == null)
        {
            return NotFound(new { error = "No active subscription found" });
        }

        return Ok(MapToSubscriptionDto(subscription));
    }

    /// <summary>
    /// Get available subscription plans
    /// </summary>
    /// <returns>List of subscription plans</returns>
    [HttpGet("plans")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<object>), StatusCodes.Status200OK)]
    public IActionResult GetPlans()
    {
        var plans = new[]
        {
            new
            {
                Type = "Free",
                Price = 0m,
                Credits = 10,
                Features = new[] { "10 free credits", "Basic profile", "QR code generation" }
            },
            new
            {
                Type = "Basic",
                Price = 9.99m,
                Credits = 50,
                Features = new[] { "50 credits/month", "Enhanced profile", "Priority support", "Analytics" }
            },
            new
            {
                Type = "Premium",
                Price = 29.99m,
                Credits = 200,
                Features = new[] { "200 credits/month", "Premium profile", "Advertising options", "Advanced analytics", "Priority support" }
            },
            new
            {
                Type = "Enterprise",
                Price = 99.99m,
                Credits = 1000,
                Features = new[] { "1000 credits/month", "Unlimited profile views", "Premium advertising", "API access", "Dedicated support" }
            }
        };

        return Ok(plans);
    }

    /// <summary>
    /// Upgrade subscription plan
    /// </summary>
    /// <param name="planType">New plan type (Basic/Premium/Enterprise)</param>
    /// <returns>Updated subscription details</returns>
    [HttpPost("upgrade")]
    [ProducesResponseType(typeof(SubscriptionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> UpgradeSubscription([FromQuery] string planType)
    {
        var validPlans = new[] { "Basic", "Premium", "Enterprise" };
        if (!validPlans.Contains(planType))
        {
            return BadRequest(new { error = "Invalid plan type" });
        }

        var userId = GetCurrentUserId();
        var currentSubscription = await _subscriptionRepository.GetActiveSubscriptionAsync(userId);
        
        if (currentSubscription == null)
        {
            return NotFound(new { error = "No active subscription found" });
        }

        // Deactivate current subscription
        currentSubscription.IsActive = false;
        await _subscriptionRepository.UpdateAsync(currentSubscription);

        // Create new subscription
        var newSubscription = new QRBusinessExchange.Core.Entities.Subscription
        {
            UserId = userId,
            SubscriptionType = planType,
            CreditsAvailable = GetCreditsForPlan(planType),
            CreditsUsed = 0,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddMonths(1),
            AutoRenew = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var createdSubscription = await _subscriptionRepository.CreateAsync(newSubscription);
        
        return Ok(MapToSubscriptionDto(createdSubscription));
    }

    /// <summary>
    /// Get available credits
    /// </summary>
    /// <returns>Credit balance information</returns>
    [HttpGet("credits")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCredits()
    {
        var userId = GetCurrentUserId();
        var subscription = await _subscriptionRepository.GetActiveSubscriptionAsync(userId);
        
        if (subscription == null)
        {
            return Ok(new { available = 0, used = 0, total = 0 });
        }

        return Ok(new
        {
            available = subscription.CreditsAvailable,
            used = subscription.CreditsUsed,
            total = subscription.CreditsAvailable + subscription.CreditsUsed
        });
    }

    /// <summary>
    /// Get credit usage history
    /// </summary>
    /// <returns>Credit usage details</returns>
    [HttpGet("usage")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsage()
    {
        var userId = GetCurrentUserId();
        var subscription = await _subscriptionRepository.GetActiveSubscriptionAsync(userId);
        
        if (subscription == null)
        {
            return NotFound(new { error = "No active subscription found" });
        }

        return Ok(new
        {
            subscriptionType = subscription.SubscriptionType,
            creditsAvailable = subscription.CreditsAvailable,
            creditsUsed = subscription.CreditsUsed,
            startDate = subscription.StartDate,
            endDate = subscription.EndDate,
            daysRemaining = subscription.EndDate.HasValue 
                ? (subscription.EndDate.Value - DateTime.UtcNow).Days 
                : 0
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

    private static int GetCreditsForPlan(string planType)
    {
        return planType switch
        {
            "Basic" => 50,
            "Premium" => 200,
            "Enterprise" => 1000,
            _ => 10
        };
    }

    private static SubscriptionDto MapToSubscriptionDto(QRBusinessExchange.Core.Entities.Subscription subscription)
    {
        return new SubscriptionDto
        {
            SubscriptionId = subscription.SubscriptionId,
            SubscriptionType = subscription.SubscriptionType,
            CreditsAvailable = subscription.CreditsAvailable,
            CreditsUsed = subscription.CreditsUsed,
            StartDate = subscription.StartDate,
            EndDate = subscription.EndDate,
            AutoRenew = subscription.AutoRenew,
            IsActive = subscription.IsActive
        };
    }
}
