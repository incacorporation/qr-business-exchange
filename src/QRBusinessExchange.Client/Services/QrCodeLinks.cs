using System.Text.RegularExpressions;

namespace QRBusinessExchange.Client.Services;

/// <summary>
/// Decides which scanned QR codes the in-app scanner may open. Only links back into this site's
/// QR flows are allowed, so a code in an email or on a poster can't send the user to another site
/// or to an arbitrary page while they're signed in.
/// </summary>
public static partial class QrCodeLinks
{
    private const string Guid = "[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}";

    // /qr/approve/{id}  - approve a sign-in shown on another screen (QrLoginService.ApproveUrl)
    // /qr/link/{id}     - link this phone with a passkey (DeviceLinkService.LinkUrl, secret in #k=...)
    // /user/{id}        - someone's business card (QRCodeCard: their public profile)
    [GeneratedRegex($"^/(qr/(approve|link)|user)/{Guid}$")]
    private static partial Regex AllowedPath();

    [GeneratedRegex($"^/user/(?<id>{Guid})$")]
    private static partial Regex BusinessCardPath();

    /// <summary>
    /// Returns the in-app route (path plus #fragment) for a scanned qrbound code, or null if the text
    /// isn't a link to one of this site's QR flows.
    /// </summary>
    /// <param name="scannedText">The decoded QR code text.</param>
    /// <param name="siteBaseUri">This app's base address (NavigationManager.BaseUri).</param>
    public static string? ToAppRoute(string? scannedText, Uri siteBaseUri)
    {
        var uri = ParseOwnLink(scannedText, siteBaseUri);
        return uri != null && AllowedPath().IsMatch(uri.AbsolutePath)
            ? uri.AbsolutePath + uri.Fragment
            : null;
    }

    /// <summary>
    /// Returns the user ID of a scanned qrbound business card (a link to /user/{id} on this site), or null.
    /// </summary>
    public static System.Guid? ToBusinessCardUserId(string? scannedText, Uri siteBaseUri)
    {
        var uri = ParseOwnLink(scannedText, siteBaseUri);
        var match = uri == null ? null : BusinessCardPath().Match(uri.AbsolutePath);
        return match is { Success: true } && string.IsNullOrEmpty(uri!.Fragment)
            ? System.Guid.Parse(match.Groups["id"].Value)
            : null;
    }

    // An absolute link to this site with no user info or query string.
    private static Uri? ParseOwnLink(string? scannedText, Uri siteBaseUri) =>
        !string.IsNullOrWhiteSpace(scannedText) &&
        Uri.TryCreate(scannedText.Trim(), UriKind.Absolute, out var uri) &&
        string.IsNullOrEmpty(uri.UserInfo) &&
        string.IsNullOrEmpty(uri.Query) &&
        SameSite(uri, siteBaseUri)
            ? uri
            : null;

    // Same scheme and port, and the same host, treating "www.example.com" and "example.com" as one site.
    private static bool SameSite(Uri a, Uri b) =>
        a.Scheme == b.Scheme &&
        a.Port == b.Port &&
        string.Equals(StripWww(a.IdnHost), StripWww(b.IdnHost), StringComparison.OrdinalIgnoreCase);

    private static string StripWww(string host) =>
        host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? host[4..] : host;
}
