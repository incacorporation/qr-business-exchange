using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Small helpers for the opaque secrets used by the SSO core (client secrets,
/// authorization codes, QR poll secrets). Only SHA-256 hashes are persisted.
/// </summary>
public static class SsoCrypto
{
    /// <summary>Creates a URL-safe random secret.</summary>
    public static string NewSecret(int bytes = 32) =>
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(bytes));

    /// <summary>Hex-encoded SHA-256 of the value.</summary>
    public static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    /// <summary>Constant-time comparison of a raw value against a stored hash.</summary>
    public static bool Matches(string? value, string? storedHash)
    {
        if (string.IsNullOrEmpty(value) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(Hash(value)),
            Encoding.ASCII.GetBytes(storedHash.ToUpperInvariant()));
    }

    /// <summary>PKCE S256: BASE64URL(SHA256(ASCII(code_verifier))).</summary>
    public static string PkceS256(string codeVerifier) =>
        Base64UrlEncoder.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
}
