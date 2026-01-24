namespace QRBusinessExchange.Infrastructure.Configuration;

/// <summary>
/// Configuration settings for JSON Web Token (JWT) authentication
/// </summary>
public class JwtSettings
{
    /// <summary>
    /// Gets or sets the secret key used for signing JWT tokens
    /// </summary>
    /// <remarks>
    /// This should be a strong, randomly generated string stored securely in configuration.
    /// Minimum recommended length is 256 bits (32 characters).
    /// </remarks>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the issuer of the JWT token
    /// </summary>
    /// <remarks>
    /// Typically the domain name of the application (e.g., "qrbusinessexchange.com")
    /// </remarks>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the intended audience for the JWT token
    /// </summary>
    /// <remarks>
    /// Typically the client application or API endpoint that will consume the token
    /// </remarks>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the expiration time for access tokens in minutes
    /// </summary>
    /// <remarks>
    /// Common values: 15-60 minutes for access tokens
    /// </remarks>
    public int ExpirationMinutes { get; set; } = 60;

    /// <summary>
    /// Gets or sets the expiration time for refresh tokens in days
    /// </summary>
    /// <remarks>
    /// Common values: 7-30 days for refresh tokens
    /// </remarks>
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
