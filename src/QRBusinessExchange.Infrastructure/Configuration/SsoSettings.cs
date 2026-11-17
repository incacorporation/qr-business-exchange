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

    /// <summary>
    /// Extra origins the portal is served from (besides <see cref="PortalBaseUrl"/>), e.g. the Web host
    /// in development. Only these origins may collect a portal session for a QR request that was made
    /// for an application.
    /// </summary>
    public List<string> PortalOrigins { get; set; } = new();

    /// <summary>True when <paramref name="origin"/> is the portal (scheme, host and port match).</summary>
    public bool IsPortalOrigin(string? origin)
    {
        if (string.IsNullOrEmpty(origin) || !Uri.TryCreate(origin, UriKind.Absolute, out var candidate))
        {
            return false;
        }

        return PortalOrigins.Append(PortalBaseUrl).Any(allowed =>
            Uri.TryCreate(allowed, UriKind.Absolute, out var a) &&
            string.Equals(a.GetLeftPart(UriPartial.Authority), candidate.GetLeftPart(UriPartial.Authority),
                StringComparison.OrdinalIgnoreCase));
    }
}
