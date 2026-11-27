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
    /// Anyone who knows this key can create sign-in tokens for any account, so it must never be in
    /// source code or appsettings.json. Set it with <c>dotnet user-secrets set "JwtSettings:SecretKey" ...</c>
    /// in development and the <c>JwtSettings__SecretKey</c> environment variable on servers.
    /// Use at least 32 random bytes, for example 64 random bytes in Base64.
    /// </remarks>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Smallest accepted key: 256 bits, the HMAC-SHA256 output size.</summary>
    public const int MinimumKeyBytes = 32;

    /// <summary>Text from sample keys that were published in this repository or its docs.</summary>
    private static readonly string[] KnownPlaceholders =
    [
        "YourSuperSecretKey", "your-secret-key", "your-production-secret", "SecretKeyFor", "changeme", "change-me"
    ];

    /// <summary>
    /// Returns the signing-key bytes, or throws if the key is missing, too short or a published placeholder.
    /// Called at startup so a misconfigured server fails immediately instead of issuing forgeable tokens.
    /// </summary>
    public static byte[] GetValidatedKeyBytes(string? secretKey)
    {
        if (string.IsNullOrWhiteSpace(secretKey))
        {
            throw new InvalidOperationException(
                "JwtSettings:SecretKey is not configured. Set it with 'dotnet user-secrets set \"JwtSettings:SecretKey\" <key>' " +
                "(development) or the JwtSettings__SecretKey environment variable (servers).");
        }

        if (KnownPlaceholders.Any(p => secretKey.Contains(p, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "JwtSettings:SecretKey is a published sample value. Anyone could forge sign-in tokens with it; generate a new random key.");
        }

        var bytes = System.Text.Encoding.UTF8.GetBytes(secretKey);
        if (bytes.Length < MinimumKeyBytes)
        {
            throw new InvalidOperationException(
                $"JwtSettings:SecretKey is too short ({bytes.Length} bytes). Use at least {MinimumKeyBytes} random bytes.");
        }

        return bytes;
    }

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
