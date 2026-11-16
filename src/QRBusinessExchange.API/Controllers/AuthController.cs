using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.DTOs;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for user authentication and authorization
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    /// <summary>How often one email address can be sent a reset link (stops mail-bombing).</summary>
    private static readonly TimeSpan ResetEmailInterval = TimeSpan.FromMinutes(2);

    private const string ResetRequestedMessage =
        "If an account exists for that email, we've sent it a link to reset the password.";

    private readonly IUserRepository _userRepository;
    private readonly JwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;
    private readonly PasswordResetTokens _resetTokens;
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SsoSettings _ssoSettings;
    private readonly IWebHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public AuthController(
        IUserRepository userRepository,
        JwtService jwtService,
        IEmailService emailService,
        ILogger<AuthController> logger,
        PasswordResetTokens resetTokens,
        IMemoryCache cache,
        IServiceScopeFactory scopeFactory,
        IOptions<SsoSettings> ssoSettings,
        IWebHostEnvironment environment,
        IConfiguration configuration)
    {
        _configuration = configuration;
        _userRepository = userRepository;
        _jwtService = jwtService;
        _emailService = emailService;
        _logger = logger;
        _resetTokens = resetTokens;
        _cache = cache;
        _scopeFactory = scopeFactory;
        _ssoSettings = ssoSettings.Value;
        _environment = environment;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>Authentication response with JWT tokens</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(Core.DTOs.AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] Core.DTOs.RegisterRequest request)
    {
        // Check if email already exists
        var existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return BadRequest(new { error = "Email already registered" });
        }

        // Text-message consent needs a number to apply to.
        var phone = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        if (request.SmsOptIn && phone == null)
        {
            return BadRequest(new { error = "Enter your mobile number to get text messages, or untick the text message box." });
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create user
        var now = DateTime.UtcNow;
        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyName = request.CompanyName,
            PhoneNumber = phone,
            // Record of consent: only when the box was ticked (it is unticked by default).
            SmsOptIn = request.SmsOptIn,
            SmsOptInAt = request.SmsOptIn ? now : null,
            SmsOptInPhone = request.SmsOptIn ? phone : null,
            SmsOptInIp = request.SmsOptIn ? HttpContext.Connection.RemoteIpAddress?.ToString() : null,
            IsEmailVerified = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdUser = await _userRepository.CreateAsync(user);

        // Generate verification token
        var verificationToken = Guid.NewGuid().ToString();
        
        // Send verification email (non-blocking)
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailService.SendVerificationEmailAsync(createdUser.UserId, createdUser.Email, verificationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send verification email to {Email}", createdUser.Email);
            }
        });

        // Generate tokens
        var accessToken = _jwtService.GenerateAccessToken(createdUser.UserId, createdUser.Email, new Dictionary<string, string>
        {
            { "role", createdUser.Role }
        });
        var refreshToken = _jwtService.GenerateRefreshToken();

        var response = new Core.DTOs.AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = MapToUserDto(createdUser)
        };

        return CreatedAtAction(nameof(Register), response);
    }

    /// <summary>
    /// Login with email and password
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>Authentication response with JWT tokens</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(Core.DTOs.AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] Core.DTOs.LoginRequest request)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email);
        if (user == null)
        {
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // Verify password
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { error = "Invalid email or password" });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { error = "Account is deactivated" });
        }

        // Update last login
        user.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        // Generate tokens
        var accessToken = _jwtService.GenerateAccessToken(user.UserId, user.Email, new Dictionary<string, string>
        {
            { "role", user.Role }
        });
        var refreshToken = _jwtService.GenerateRefreshToken();

        var response = new Core.DTOs.AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = MapToUserDto(user)
        };

        return Ok(response);
    }

    /// <summary>
    /// Verify email address using verification token
    /// </summary>
    /// <param name="token">Email verification token</param>
    /// <returns>Success or error message</returns>
    [HttpPost("verify-email")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> VerifyEmail([FromQuery] string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            return BadRequest(new { error = "Invalid verification token" });
        }

        // In a real implementation, you would look up the token in EmailVerification table
        // For now, we'll just return success
        return Ok(new { message = "Email verified successfully" });
    }

    /// <summary>
    /// Resend email verification
    /// </summary>
    /// <param name="email">User email address</param>
    /// <returns>Success or error message</returns>
    [HttpPost("resend-verification")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResendVerification([FromQuery] string email)
    {
        var user = await _userRepository.GetByEmailAsync(email);
        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        if (user.IsEmailVerified)
        {
            return BadRequest(new { error = "Email already verified" });
        }

        var verificationToken = Guid.NewGuid().ToString();
        
        await _emailService.SendVerificationEmailAsync(user.UserId, user.Email, verificationToken);

        return Ok(new { message = "Verification email sent successfully" });
    }

    /// <summary>
    /// Email a password-reset link. Always answers the same way, whether or not the account exists,
    /// so this can't be used to find out which emails are registered.
    /// </summary>
    [HttpPost("forgot-password")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult ForgotPassword([FromBody] Core.DTOs.ForgotPasswordRequest request)
    {
        var email = request.Email.Trim();

        // One email per address per interval; extra requests get the same answer but send nothing.
        var throttleKey = $"pwreset:{email.ToLowerInvariant()}";
        if (_cache.TryGetValue(throttleKey, out _))
        {
            return Accepted(new { message = ResetRequestedMessage });
        }
        _cache.Set(throttleKey, true, ResetEmailInterval);

        // Look up and send in the background so the response time doesn't reveal whether the account exists.
        var portalBaseUrl = _ssoSettings.PortalBaseUrl.TrimEnd('/');
        var logLinkForDevelopment = _environment.IsDevelopment();
        _ = Task.Run(() => SendResetLinkAsync(email, portalBaseUrl, logLinkForDevelopment));

        return Accepted(new { message = ResetRequestedMessage });
    }

    /// <summary>
    /// Set a new password using the token from a reset link. The link stops working once used.
    /// </summary>
    [HttpPost("reset-password")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ResetPassword([FromBody] Core.DTOs.ResetPasswordRequest request)
    {
        var userId = _resetTokens.ReadUserId(request.Token, out var fingerprint);
        var user = userId is Guid id ? await _userRepository.GetByIdAsync(id) : null;

        if (user == null || !user.IsActive || !PasswordResetTokens.MatchesCurrentPassword(user, fingerprint))
        {
            return BadRequest(new
            {
                error = "invalid_token",
                error_description = "This reset link is invalid, expired or already used. Request a new one."
            });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.IsEmailVerified = true; // opening the emailed link proves they own the address
        user.UpdatedAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);

        _logger.LogInformation("Password reset completed for user {UserId}", user.UserId);

        var forgotUrl = $"{_ssoSettings.PortalBaseUrl.TrimEnd('/')}/forgot-password";
        var (email, firstName) = (user.Email, user.FirstName);
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                await emailService.SendPasswordChangedEmailAsync(email, firstName, forgotUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send password-changed notice for user {UserId}", user.UserId);
            }
        });

        return Ok(new { message = "Your password has been changed. You can log in with it now." });
    }

    private async Task SendResetLinkAsync(string email, string portalBaseUrl, bool logLinkForDevelopment)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var users = scope.ServiceProvider.GetRequiredService<IUserRepository>();
            var user = await users.GetByEmailAsync(email);
            if (user == null || !user.IsActive)
            {
                return;
            }

            var token = _resetTokens.Create(user, DateTimeOffset.UtcNow);
            var resetUrl = $"{portalBaseUrl}/reset-password#token={Uri.EscapeDataString(token)}";

            if (logLinkForDevelopment)
            {
                // Development only: lets you test without a working mail server. Never logged in production.
                _logger.LogInformation("Development password-reset link for {UserId}: {ResetUrl}", user.UserId, resetUrl);
            }

            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
            var sent = await emailService.SendPasswordResetEmailAsync(user.Email, user.FirstName, resetUrl, PasswordResetTokens.Lifetime);
            if (!sent)
            {
                _logger.LogWarning("Password-reset email for user {UserId} could not be sent", user.UserId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send a password-reset email");
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New authentication response with JWT tokens</returns>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(Core.DTOs.AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        // In a real implementation, validate refresh token against database
        // For now, return unauthorized
        return Unauthorized(new { error = "Invalid refresh token" });
    }

    /// <summary>
    /// Logout and invalidate refresh token
    /// </summary>
    /// <returns>Success message</returns>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult Logout()
    {
        // In a real implementation, invalidate the refresh token in database
        return Ok(new { message = "Logged out successfully" });
    }

    /// <summary>
    /// Register a new admin user (protected by admin secret key)
    /// </summary>
    /// <param name="request">Admin registration details</param>
    /// <param name="adminSecret">Admin secret key from configuration</param>
    /// <returns>Authentication response with JWT tokens</returns>
    [HttpPost("register-admin")]
    [ProducesResponseType(typeof(Core.DTOs.AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegisterAdmin(
        [FromBody] Core.DTOs.RegisterRequest request,
        [FromHeader(Name = "X-Admin-Secret")] string? adminSecret)
    {
        // The secret comes only from user-secrets / the Admin__RegistrationSecret environment variable.
        // With none configured (the default) admin self-registration is switched off entirely.
        var configuredSecret = _configuration["Admin:RegistrationSecret"];
        if (string.IsNullOrWhiteSpace(configuredSecret) || configuredSecret.Length < 24)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(adminSecret) ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(adminSecret),
                System.Text.Encoding.UTF8.GetBytes(configuredSecret)))
        {
            _logger.LogWarning("Rejected admin registration attempt from {RemoteIp}", HttpContext.Connection.RemoteIpAddress);
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Invalid or missing admin secret" });
        }

        // Check if email already exists
        var existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return BadRequest(new { error = "Email already registered" });
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create admin user
        var admin = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyName = request.CompanyName,
            PhoneNumber = request.PhoneNumber,
            Role = "Admin", // Set role to Admin
            IsEmailVerified = true, // Auto-verify admin accounts
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var createdAdmin = await _userRepository.CreateAsync(admin);

        _logger.LogInformation("Admin user created: {Email}", createdAdmin.Email);

        // Generate tokens with admin role
        var accessToken = _jwtService.GenerateAccessToken(createdAdmin.UserId, createdAdmin.Email, new Dictionary<string, string>
        {
            { "role", createdAdmin.Role }
        });
        var refreshToken = _jwtService.GenerateRefreshToken();

        var response = new Core.DTOs.AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = MapToUserDto(createdAdmin)
        };

        return CreatedAtAction(nameof(RegisterAdmin), response);
    }

    private static UserProfileDto MapToUserDto(User user)
    {
        return new UserProfileDto
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
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }
}
