using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace QRBusinessExchange.API.Extensions;

/// <summary>
/// Reads values from the core (portal) JWT regardless of whether inbound claim
/// mapping turned "sub"/"amr" into their long ClaimTypes URIs.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    /// <summary>"passkey", "qr" or "pwd".</summary>
    public static string GetAuthMethod(this ClaimsPrincipal user) =>
        user.FindFirst(JwtRegisteredClaimNames.Amr)?.Value
        // JwtBearer's default inbound mapping renames "amr" to this URI
        ?? user.FindFirst("http://schemas.microsoft.com/claims/authnmethodsreferences")?.Value
        ?? user.FindFirst(ClaimTypes.AuthenticationMethod)?.Value
        ?? "pwd";

    public static DateTime GetAuthTime(this ClaimsPrincipal user)
    {
        var raw = user.FindFirst(JwtRegisteredClaimNames.AuthTime)?.Value
                  ?? user.FindFirst(ClaimTypes.AuthenticationInstant)?.Value
                  ?? user.FindFirst(JwtRegisteredClaimNames.Iat)?.Value;
        return long.TryParse(raw, out var seconds)
            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
            : DateTime.UtcNow;
    }
}
