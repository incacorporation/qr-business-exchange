using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Qrbound.SignIn.Tests;

/// <summary>Issues tokens shaped like the ones the API's SsoService creates.</summary>
internal sealed class TestTokens
{
    public const string Issuer = "https://webapi.test";
    public const string ClientId = "app_test";

    public RsaSecurityKey Key { get; } = new(RSA.Create(2048)) { KeyId = "k1" };

    public string Create(string tokenUse = "id", string? nonce = "n-1", string audience = ClientId,
        string issuer = Issuer, DateTime? expires = null, RsaSecurityKey? signWith = null,
        IDictionary<string, object>? extra = null)
    {
        var now = DateTime.UtcNow;
        var claims = new Dictionary<string, object>
        {
            ["sub"] = "11111111-2222-3333-4444-555555555555",
            ["email"] = "user@example.test",
            ["email_verified"] = true,
            ["name"] = "Test User",
            ["given_name"] = "Test",
            ["family_name"] = "User",
            ["amr"] = "qr",
            ["client_id"] = ClientId,
            ["business_id"] = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
            ["business_slug"] = "acme",
            ["business_role"] = "Admin",
            ["token_use"] = tokenUse
        };
        if (nonce != null) claims["nonce"] = nonce;
        if (extra != null) foreach (var (k, v) in extra) claims[k] = v;

        var exp = expires ?? now.AddMinutes(60);
        var start = exp < now ? exp.AddMinutes(-10) : now;
        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Claims = claims,
            NotBefore = start,
            IssuedAt = start,
            Expires = exp,
            SigningCredentials = new SigningCredentials(signWith ?? Key, SecurityAlgorithms.RsaSha256)
        });
    }
}
