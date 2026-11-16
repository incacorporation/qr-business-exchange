using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Health check controller for database connectivity
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HealthController> _logger;

    public HealthController(
        ApplicationDbContext context,
        ILogger<HealthController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Check API health
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetHealth()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            service = "QR Business Exchange API"
        });
    }

    /// <summary>
    /// Check database connectivity
    /// </summary>
    [HttpGet("database")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetDatabaseHealth()
    {
        try
        {
            // Try to connect to database
            var canConnect = await _context.Database.CanConnectAsync();
            
            if (!canConnect)
            {
                return StatusCode(500, new
                {
                    status = "unhealthy",
                    database = "disconnected",
                    message = "Cannot connect to database",
                    timestamp = DateTime.UtcNow
                });
            }            
            // Count users (to verify table exists)
            var userCount = await _context.Users.CountAsync();

            return Ok(new
            {
                status = "healthy",
                database = "connected",
                userCount = userCount,
                databaseName = _context.Database.GetDbConnection().Database,
                connectionState = _context.Database.GetDbConnection().State.ToString(),
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            
            return StatusCode(500, new
            {
                status = "unhealthy",
                database = "error",
                error = ex.Message,
                innerError = ex.InnerException?.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// List all users (for debugging - remove in production!)
    /// </summary>
    [HttpGet("users")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _context.Users
                .Select(u => new
                {
                    u.UserId,
                    u.Email,
                    u.FirstName,
                    u.LastName,
                    u.Role,
                    u.IsActive,
                    u.IsEmailVerified,
                    u.CreatedAt
                })
                .Take(10) // Limit to 10 users
                .ToListAsync();

            return Ok(new
            {
                count = users.Count,
                users = users,
                note = "Remove this endpoint in production!"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing users");
            
            return StatusCode(500, new
            {
                error = ex.Message,
                innerError = ex.InnerException?.Message
            });
        }
    }
}
