using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;
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
    private readonly EmailSettings _email;
    private readonly IEmailService _emailService;
    private readonly IUserRepository _users;
    private readonly IConfiguration? _configuration;

    public HealthController(
        ApplicationDbContext context,
        ILogger<HealthController> logger,
        IOptions<EmailSettings> email,
        IEmailService emailService,
        IUserRepository users,
        IConfiguration? configuration = null)
    {
        _configuration = configuration;
        _context = context;
        _logger = logger;
        _email = email.Value;
        _emailService = emailService;
        _users = users;
    }

    /// <summary>
    /// Whether email sending is set up: shows the mail server and sender, and only whether a password is
    /// set (never the password).
    /// </summary>
    [HttpGet("email")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetEmailSetup()
    {
        var passwordSet = !string.IsNullOrWhiteSpace(_email.SmtpPassword);
        var complete = passwordSet && !string.IsNullOrWhiteSpace(_email.SmtpHost) && _email.SmtpPort > 0
                       && !string.IsNullOrWhiteSpace(_email.SmtpUser) && !string.IsNullOrWhiteSpace(_email.FromEmail);
        return Ok(new
        {
            status = complete ? "configured" : "incomplete",
            smtpHost = _email.SmtpHost,
            smtpPort = _email.SmtpPort,
            smtpUser = _email.SmtpUser,
            fromEmail = _email.FromEmail,
            passwordSet
        });
    }

    /// <summary>
    /// Admins only: sends a test email now and returns the mail server's error if it fails.
    /// </summary>
    [HttpPost("email-test")]
    [Authorize]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SendTestEmail([FromBody] EmailTestRequest request)
    {
        var subject = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? User.FindFirst("sub")?.Value;
        var caller = Guid.TryParse(subject, out var id) ? await _users.GetByIdAsync(id) : null;
        if (caller is null || caller.Role is not ("Admin" or "SuperAdmin"))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Only qrbound admins can send test emails." });
        }

        if (string.IsNullOrWhiteSpace(request.To) || !System.Net.Mail.MailAddress.TryCreate(request.To.Trim(), out _))
        {
            return BadRequest(new { error = "Enter a valid email address." });
        }

        var error = await _emailService.SendTestEmailAsync(request.To.Trim());
        return Ok(new { sent = error == null, error, password = DescribePassword(request.Salt) });
    }

    /// <summary>
    /// Where the SMTP password the API is actually using comes from, and a fingerprint of it, so it can be
    /// compared with a password known to work without either being shown. The fingerprint is an HMAC keyed
    /// with a random salt chosen by the (admin) caller, cut to 8 hex characters.
    /// </summary>
    private object DescribePassword(string? salt)
    {
        const string key = "EmailSettings:SmtpPassword";
        string? source = null;
        if (_configuration is IConfigurationRoot root)
        {
            // Later providers win, so the last one that has the key is where the value came from.
            var provider = root.Providers.LastOrDefault(p => p.TryGet(key, out _));
            source = provider?.ToString();
        }

        var password = _email.SmtpPassword ?? string.Empty;
        string? fingerprint = null;
        if (!string.IsNullOrEmpty(salt) && salt.Length >= 16 && password.Length > 0)
        {
            using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(salt));
            fingerprint = Convert.ToHexString(hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password)))[..8].ToLowerInvariant();
        }

        return new { source, length = password.Length, fingerprint };
    }

    public sealed class EmailTestRequest
    {
        public string To { get; set; } = string.Empty;

        /// <summary>Optional random text (16+ characters) for the password fingerprint.</summary>
        public string? Salt { get; set; }
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
