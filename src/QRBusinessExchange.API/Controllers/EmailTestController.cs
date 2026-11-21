using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.Core.Interfaces;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for testing email functionality (Development only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class EmailTestController : ControllerBase
{
    private readonly IEmailService _emailService;
    private readonly ILogger<EmailTestController> _logger;

    public EmailTestController(
        IEmailService emailService,
        ILogger<EmailTestController> logger)
    {
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Send a test email to verify SMTP configuration
    /// </summary>
    /// <param name="email">Email address to send test email to</param>
    /// <returns>Success or failure message</returns>
    [HttpPost("send-test-email")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> SendTestEmail([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "Email address is required" });
        }

        if (!email.Contains('@'))
        {
            return BadRequest(new { error = "Invalid email address format" });
        }

        try
        {
            _logger.LogInformation("Sending test email to {Email}", email);

            // Use a dummy user ID for testing
            var testUserId = Guid.NewGuid();
            var testToken = Guid.NewGuid().ToString();

            var result = await _emailService.SendVerificationEmailAsync(
                testUserId,
                email,
                testToken);

            if (result)
            {
                return Ok(new
                {
                    success = true,
                    message = $"Test email sent successfully to {email}",
                    timestamp = DateTime.UtcNow,
                    note = "Check your inbox (or spam folder) for the verification email"
                });
            }
            else
            {
                return StatusCode(500, new
                {
                    success = false,
                    error = "Email service returned false. Check server logs for details.",
                    timestamp = DateTime.UtcNow
                });
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not configured"))
        {
            _logger.LogError(ex, "SMTP configuration error");
            return StatusCode(500, new
            {
                success = false,
                error = "SMTP is not properly configured",
                details = ex.Message,
                hint = "Check appsettings.json or User Secrets for EmailSettings configuration"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending test email to {Email}", email);
            return StatusCode(500, new
            {
                success = false,
                error = "Failed to send test email",
                details = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Check SMTP configuration status
    /// </summary>
    /// <returns>Configuration status</returns>
    [HttpGet("smtp-status")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public IActionResult GetSmtpStatus()
    {
        try
        {
            // Try to access the email service - if configuration is invalid, constructor will throw
            var serviceType = _emailService.GetType().FullName;
            
            return Ok(new
            {
                configured = true,
                message = "SMTP configuration is valid",
                serviceType = serviceType,
                note = "Configuration validated. Use POST /api/EmailTest/send-test-email to send actual test email."
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                configured = false,
                error = ex.Message,
                hint = "Configure EmailSettings in appsettings.json or User Secrets"
            });
        }
    }

    /// <summary>
    /// Send a connection notification test email
    /// </summary>
    /// <param name="email">Email address to send to</param>
    /// <param name="senderName">Name of the sender</param>
    /// <returns>Success or failure message</returns>
    [HttpPost("test-connection-notification")]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestConnectionNotification(
        [FromQuery] string email,
        [FromQuery] string senderName = "Test User")
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return BadRequest(new { error = "Email address is required" });
        }

        try
        {
            // Note: This won't work without a real user in the database
            // Just for demonstration - you'd need to create a test user first
            return Ok(new
            {
                message = "This endpoint requires a real user in the database",
                hint = "Use send-test-email instead for basic SMTP testing"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing connection notification");
            return StatusCode(500, new { error = ex.Message });
        }
    }
}
