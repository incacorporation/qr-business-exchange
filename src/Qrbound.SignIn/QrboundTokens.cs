using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Qrbound.SignIn;

/// <summary>Token response from qrbound's token endpoint.</summary>
public sealed class QrboundTokenResponse
{
    [JsonPropertyName("access_token")] public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("id_token")] public string IdToken { get; set; } = string.Empty;
    [JsonPropertyName("token_type")] public string TokenType { get; set; } = "Bearer";
    [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
}

/// <summary>The signed-in qrbound user, read from the ID token.</summary>
public sealed class QrboundUser
{
    public string Subject { get; init; } = string.Empty;
    public string? Email { get; init; }
    public bool EmailVerified { get; init; }
    public string? Name { get; init; }
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    /// <summary>How the user authenticated at qrbound: "qr" or "passkey".</summary>
    public string? AuthMethod { get; init; }
    public Guid? BusinessId { get; init; }
    public string? BusinessSlug { get; init; }
    /// <summary>The user's role in the business that owns your app (absent when not a member).</summary>
    public string? BusinessRole { get; init; }
    public DateTimeOffset ExpiresAt { get; init; }
    /// <summary>All claims of the ID token.</summary>
    public IReadOnlyList<Claim> Claims { get; init; } = Array.Empty<Claim>();

    /// <summary>Builds a <see cref="ClaimsPrincipal"/> (authentication type "qrbound").</summary>
    public ClaimsPrincipal ToPrincipal(string authenticationType = "qrbound") =>
        new(new ClaimsIdentity(Claims, authenticationType, "name", "business_role"));
}

/// <summary>Raised when a token fails validation.</summary>
public sealed class QrboundTokenException(string message) : Exception(message);

/// <summary>
/// Reads and checks a qrbound ID token's claims (issuer, audience, lifetime, nonce, token_use).
/// <para>
/// This does <b>not</b> verify the RS256 signature: browsers running .NET on WebAssembly have no RSA.
/// That is acceptable only because the token was received directly from qrbound's token endpoint over
/// HTTPS (OpenID Connect Core 3.1.3.7). Servers should use <c>QrboundTokenValidator</c> from
/// Qrbound.SignIn.Server, which checks the signature against qrbound's published keys.
/// </para>
/// </summary>
public static class QrboundIdTokenReader
{
    /// <summary>Decodes the JWT payload without any checks.</summary>
    /// <exception cref="QrboundTokenException">The value is not a JWT.</exception>
    public static JsonElement DecodePayload(string jwt)
    {
        var parts = jwt?.Split('.') ?? Array.Empty<string>();
        if (parts.Length != 3)
        {
            throw new QrboundTokenException("The token is not a JWT.");
        }

        try
        {
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(Base64Url.Decode(parts[1])));
            return doc.RootElement.Clone();
        }
        catch (Exception ex) when (ex is FormatException or JsonException or ArgumentException)
        {
            throw new QrboundTokenException("The token payload could not be read.");
        }
    }

    /// <summary>Checks the claims of an ID token and returns the user.</summary>
    /// <exception cref="QrboundTokenException">A check failed.</exception>
    public static QrboundUser Read(string idToken, string expectedIssuer, string clientId,
        string? expectedNonce, DateTimeOffset now, TimeSpan clockSkew)
    {
        var payload = DecodePayload(idToken);

        var issuer = GetString(payload, "iss");
        if (!string.Equals(issuer?.TrimEnd('/'), expectedIssuer.TrimEnd('/'), StringComparison.Ordinal))
            throw new QrboundTokenException($"Unexpected token issuer '{issuer}'.");

        if (!HasAudience(payload, clientId))
            throw new QrboundTokenException("The token was not issued to this application.");

        var exp = GetUnixTime(payload, "exp") ?? throw new QrboundTokenException("The token has no expiry.");
        if (exp + clockSkew < now)
            throw new QrboundTokenException("The token has expired.");

        var nbf = GetUnixTime(payload, "nbf");
        if (nbf.HasValue && nbf.Value - clockSkew > now)
            throw new QrboundTokenException("The token is not valid yet.");

        var tokenUse = GetString(payload, "token_use");
        if (tokenUse != null && tokenUse != "id")
            throw new QrboundTokenException("An ID token is required.");

        if (!string.IsNullOrEmpty(expectedNonce) &&
            !string.Equals(GetString(payload, "nonce"), expectedNonce, StringComparison.Ordinal))
            throw new QrboundTokenException("The token nonce does not match this sign-in.");

        var subject = GetString(payload, "sub");
        if (string.IsNullOrEmpty(subject))
            throw new QrboundTokenException("The token has no subject.");

        return new QrboundUser
        {
            Subject = subject,
            Email = GetString(payload, "email"),
            EmailVerified = string.Equals(GetString(payload, "email_verified"), "true", StringComparison.OrdinalIgnoreCase),
            Name = GetString(payload, "name"),
            GivenName = GetString(payload, "given_name"),
            FamilyName = GetString(payload, "family_name"),
            AuthMethod = GetString(payload, "amr"),
            BusinessId = Guid.TryParse(GetString(payload, "business_id"), out var b) ? b : null,
            BusinessSlug = GetString(payload, "business_slug"),
            BusinessRole = GetString(payload, "business_role"),
            ExpiresAt = exp,
            Claims = ToClaims(payload, issuer!)
        };
    }

    private static bool HasAudience(JsonElement payload, string clientId)
    {
        if (!payload.TryGetProperty("aud", out var aud)) return false;
        return aud.ValueKind switch
        {
            JsonValueKind.String => aud.GetString() == clientId,
            JsonValueKind.Array => aud.EnumerateArray().Any(a => a.ValueKind == JsonValueKind.String && a.GetString() == clientId),
            _ => false
        };
    }

    private static string? GetString(JsonElement payload, string name)
    {
        if (!payload.TryGetProperty(name, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.Array => value.EnumerateArray().Select(ClaimText).FirstOrDefault(),
            _ => null
        };
    }

    private static string ClaimText(JsonElement e) =>
        e.ValueKind == JsonValueKind.String ? e.GetString() ?? string.Empty : e.GetRawText();

    private static DateTimeOffset? GetUnixTime(JsonElement payload, string name)
    {
        if (payload.TryGetProperty(name, out var value))
        {
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var n))
                return DateTimeOffset.FromUnixTimeSeconds(n);
            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), out var s))
                return DateTimeOffset.FromUnixTimeSeconds(s);
        }
        return null;
    }

    private static List<Claim> ToClaims(JsonElement payload, string issuer)
    {
        var claims = new List<Claim>();
        foreach (var property in payload.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in property.Value.EnumerateArray())
                    claims.Add(new Claim(property.Name, ClaimText(item), ClaimValueTypes.String, issuer));
            }
            else
            {
                claims.Add(new Claim(property.Name, ClaimText(property.Value), ClaimValueTypes.String, issuer));
            }
        }
        return claims;
    }
}
