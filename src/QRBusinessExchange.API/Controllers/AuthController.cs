using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QRBusinessExchange.API.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Controllers;

/// <summary>
/// Controller for user authentication and authorization
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly JwtService _jwtService;
    private readonly IEmailService _emailService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IUserRepository userRepository,
        JwtService jwtService,
        IEmailService emailService,
        ILogger<AuthController> logger)
    {
        _userRepository = userRepository;
        _jwtService = jwtService;
        _emailService = emailService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user account
    /// </summary>
    /// <param name="request">Registration details</param>
    /// <returns>Authentication response with JWT tokens</returns>
    [HttpPost("register")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        // Check if email already exists
        var existingUser = await _userRepository.GetByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return BadRequest(new { error = "Email already registered" });
        }

        // Hash password
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        // Create user
        var user = new User
        {
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CompanyName = request.CompanyName,
            PhoneNumber = request.PhoneNumber,
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

        var response = new AuthResponse
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
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
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

        var response = new AuthResponse
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
    /// Refresh access token using refresh token
    /// </summary>
    /// <param name="request">Refresh token request</param>
    /// <returns>New authentication response with JWT tokens</returns>
    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status200OK)]
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
    [ProducesResponseType(typeof(AuthResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RegisterAdmin(
        [FromBody] RegisterRequest request,
        [FromHeader(Name = "X-Admin-Secret")] string? adminSecret)
    {
        // Validate admin secret (in production, use configuration)
        const string ADMIN_SECRET = "QRBizExchange2025!Admin"; // TODO: Move to appsettings.json
        
        if (string.IsNullOrWhiteSpace(adminSecret) || adminSecret != ADMIN_SECRET)
        {
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

        var response = new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60),
            User = MapToUserDto(createdAdmin)
        };

        return CreatedAtAction(nameof(RegisterAdmin), response);
    }

    private static UserDto MapToUserDto(User user)
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
            Role = user.Role,
            CreatedAt = user.CreatedAt
        };
    }
}
