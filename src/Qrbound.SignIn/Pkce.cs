using System.Security.Cryptography;
using System.Text;

namespace Qrbound.SignIn;

/// <summary>
/// PKCE (RFC 7636) helpers. Only the S256 method is supported, as qrbound requires it.
/// </summary>
public static class Pkce
{
    /// <summary>The only code_challenge_method qrbound accepts.</summary>
    public const string MethodS256 = "S256";

    /// <summary>
    /// Creates a new code_verifier: 32 random bytes, base64url-encoded (43 characters).
    /// </summary>
    public static string CreateVerifier() => Base64Url.Encode(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Computes the S256 code_challenge: BASE64URL(SHA256(ASCII(code_verifier))).
    /// </summary>
    /// <exception cref="ArgumentException">The verifier is not a valid RFC 7636 code_verifier.</exception>
    public static string CreateChallenge(string codeVerifier)
    {
        if (!IsValidVerifier(codeVerifier))
        {
            throw new ArgumentException(
                "A code_verifier must be 43-128 characters of A-Z, a-z, 0-9, '-', '.', '_' or '~'.",
                nameof(codeVerifier));
        }

        return Base64Url.Encode(SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier)));
    }

    /// <summary>
    /// True when <paramref name="codeVerifier"/> has the length and characters RFC 7636 section 4.1 allows.
    /// </summary>
    public static bool IsValidVerifier(string? codeVerifier)
    {
        if (codeVerifier is null || codeVerifier.Length is < 43 or > 128)
        {
            return false;
        }

        foreach (var c in codeVerifier)
        {
            var unreserved = c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9')
                or '-' or '.' or '_' or '~';
            if (!unreserved)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Checks a verifier against a challenge in constant time.</summary>
    public static bool Verify(string codeVerifier, string codeChallenge)
    {
        if (!IsValidVerifier(codeVerifier) || string.IsNullOrEmpty(codeChallenge))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(CreateChallenge(codeVerifier)),
            Encoding.ASCII.GetBytes(codeChallenge));
    }

    /// <summary>A random value for the OAuth <c>state</c> or OIDC <c>nonce</c> parameters.</summary>
    public static string CreateRandomValue(int bytes = 16) => Base64Url.Encode(RandomNumberGenerator.GetBytes(bytes));
}

/// <summary>Base64url without padding (RFC 4648 section 5).</summary>
public static class Base64Url
{
    public static string Encode(ReadOnlySpan<byte> data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <exception cref="FormatException">The input is not valid base64url.</exception>
    public static byte[] Decode(string value)
    {
        var s = value.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
            case 1: throw new FormatException("Invalid base64url length.");
        }

        return Convert.FromBase64String(s);
    }
}
