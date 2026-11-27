using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.API.Services;

/// <summary>
/// Creates and checks the tokens in email-verification links.
/// <para>
/// Same approach as <see cref="PasswordResetTokens"/>: tokens are encrypted and signed with ASP.NET Core
/// Data Protection (keys stay on the server) and expire after <see cref="Lifetime"/>. Each token carries a
/// fingerprint of the email address it was sent to, so it can only verify that address: if the account's
/// email changes, older links stop working. Using a link again after the address is verified changes
/// nothing. No database table is needed.
/// </para>
/// </summary>
public sealed class EmailVerificationTokens
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);

    private const string Purpose = "QRBusinessExchange.EmailVerification.v1";
    private readonly ITimeLimitedDataProtector _protector;

    public EmailVerificationTokens(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
    }

    public string Create(User user, DateTimeOffset now) =>
        _protector.Protect($"{user.UserId:N}|{Fingerprint(user.Email)}", now.Add(Lifetime));

    /// <summary>
    /// Returns the user id the token was issued for, or null if the token is malformed, tampered with or expired.
    /// The caller must still load the user and call <see cref="MatchesCurrentEmail"/>.
    /// </summary>
    public Guid? ReadUserId(string? token, out string? fingerprint)
    {
        fingerprint = null;
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
        {
            return null;
        }

        try
        {
            var payload = _protector.Unprotect(token.Trim(), out _);
            var parts = payload.Split('|');
            if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out var userId))
            {
                return null;
            }

            fingerprint = parts[1];
            return userId;
        }
        catch (CryptographicException)
        {
            return null; // bad signature, wrong key, other purpose (e.g. a reset token) or expired
        }
    }

    /// <summary>True while the account still has the email address the token was sent to.</summary>
    public static bool MatchesCurrentEmail(User user, string? fingerprint) =>
        fingerprint != null &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(fingerprint),
            Encoding.ASCII.GetBytes(Fingerprint(user.Email)));

    private static string Fingerprint(string email)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(email.Trim().ToLowerInvariant()));
        return Convert.ToHexString(hash, 0, 16);
    }
}
