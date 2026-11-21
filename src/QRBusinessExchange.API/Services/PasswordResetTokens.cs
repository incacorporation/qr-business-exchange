using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;
using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.API.Services;

/// <summary>
/// Creates and checks the tokens in password-reset links.
/// <para>
/// Tokens are encrypted and signed with ASP.NET Core Data Protection (keys stay on the server, never in
/// configuration) and expire after <see cref="Lifetime"/>. Each token also carries a fingerprint of the
/// account's current password hash, so it stops working as soon as the password changes: a link can be
/// used once, and every older link dies with it. No database table is needed.
/// </para>
/// </summary>
public sealed class PasswordResetTokens
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(30);

    private const string Purpose = "QRBusinessExchange.PasswordReset.v1";
    private readonly ITimeLimitedDataProtector _protector;

    public PasswordResetTokens(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector(Purpose).ToTimeLimitedDataProtector();
    }

    public string Create(User user, DateTimeOffset now) =>
        _protector.Protect($"{user.UserId:N}|{Fingerprint(user)}", now.Add(Lifetime));

    /// <summary>
    /// Returns the user id the token was issued for, or null if the token is malformed, tampered with or expired.
    /// The caller must still load the user and call <see cref="MatchesCurrentPassword"/>.
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
            return null; // bad signature, wrong key or expired
        }
    }

    /// <summary>True while the user's password is still the one the token was issued against.</summary>
    public static bool MatchesCurrentPassword(User user, string? fingerprint) =>
        fingerprint != null &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(fingerprint),
            Encoding.ASCII.GetBytes(Fingerprint(user)));

    private static string Fingerprint(User user)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{user.PasswordHash}|{user.Email.ToLowerInvariant()}|{user.IsActive}"));
        return Convert.ToHexString(hash, 0, 16);
    }
}
