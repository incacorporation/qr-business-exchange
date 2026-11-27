using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QRBusinessExchange.Infrastructure.Configuration;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Service for generating and validating JSON Web Tokens (JWT)
/// </summary>
public class JwtService
{
    private readonly JwtSettings _jwtSettings;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly SymmetricSecurityKey _signingKey;

    /// <summary>
    /// Initializes a new instance of the <see cref="JwtService"/> class
    /// </summary>
    /// <param name="jwtSettings">JWT configuration settings</param>
    public JwtService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings?.Value ?? throw new ArgumentNullException(nameof(jwtSettings));
        // Keep claim names as written ("sub", "email"); by default the handler renames "sub" to a
        // long ClaimTypes URI, so ValidateToken never found the user id and always returned null.
        _tokenHandler = new JwtSecurityTokenHandler { MapInboundClaims = false };

        _signingKey = new SymmetricSecurityKey(JwtSettings.GetValidatedKeyBytes(_jwtSettings.SecretKey));
    }

    /// <summary>
    /// Generates a JWT access token for the specified user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="email">The user's email address</param>
    /// <param name="additionalClaims">Optional additional claims to include in the token</param>
    /// <returns>A JWT access token string</returns>
    public string GenerateAccessToken(Guid userId, string email, Dictionary<string, string>? additionalClaims = null)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty", nameof(email));
        }

        try
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
            };

            if (additionalClaims != null)
            {
                foreach (var claim in additionalClaims)
                {
                    claims.Add(new Claim(claim.Key, claim.Value));
                }
            }

            var credentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _jwtSettings.Issuer,
                audience: _jwtSettings.Audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
                signingCredentials: credentials
            );

            return _tokenHandler.WriteToken(token);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error generating access token", ex);
        }
    }

    /// <summary>
    /// Generates a cryptographically secure refresh token
    /// </summary>
    /// <returns>A base64-encoded refresh token string</returns>
    public string GenerateRefreshToken()
    {
        try
        {
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            return Convert.ToBase64String(randomBytes);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error generating refresh token", ex);
        }
    }

    /// <summary>
    /// Validates a JWT token and extracts the user ID
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>The user ID if the token is valid; otherwise null</returns>
    public Guid? ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidAudience = _jwtSettings.Audience,
                IssuerSigningKey = _signingKey,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero
            };

            var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

            var userIdClaim = principal.FindFirst(JwtRegisteredClaimNames.Sub);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var userId))
            {
                return userId;
            }

            return null;
        }
        catch (SecurityTokenException)
        {
            return null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Extracts claims from a JWT token without validation
    /// </summary>
    /// <param name="token">The JWT token to parse</param>
    /// <returns>A dictionary of claims, or null if the token is invalid</returns>
    public Dictionary<string, string>? GetClaims(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        try
        {
            var jwtToken = _tokenHandler.ReadJwtToken(token);
            return jwtToken.Claims.ToDictionary(c => c.Type, c => c.Value);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Gets the expiration date of a refresh token
    /// </summary>
    /// <returns>The expiration date for refresh tokens</returns>
    public DateTime GetRefreshTokenExpiration()
    {
        return DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays);
    }
}
