namespace QRBusinessExchange.Infrastructure.Configuration;

/// <summary>
/// Configuration for the multi-business SSO / QR login core.
/// </summary>
public class SsoSettings
{
    /// <summary>
    /// "iss" claim of tokens issued to business applications (normally the API's public URL).
    /// </summary>
    public string Issuer { get; set; } = "https://localhost:7059";

    /// <summary>
    /// Public base URL of the sign-in portal (the Blazor client). Used for the
    /// authorize page and the URL encoded in login QR codes.
    /// </summary>
    public string PortalBaseUrl { get; set; } = "https://localhost:60128";

    /// <summary>
    /// Path to the PEM file holding the RSA key used to sign application tokens (RS256).
    /// Relative paths are resolved against the API content root. Generated on first run
    /// if missing — in production supply a managed key and keep it out of source control.
    /// </summary>
    public string SigningKeyPath { get; set; } = "keys/sso-signing-key.pem";

    /// <summary>Lifetime of authorization codes.</summary>
    public int AuthorizationCodeLifetimeSeconds { get; set; } = 120;

    /// <summary>Lifetime of a QR login request.</summary>
    public int QrSessionLifetimeSeconds { get; set; } = 180;
}
