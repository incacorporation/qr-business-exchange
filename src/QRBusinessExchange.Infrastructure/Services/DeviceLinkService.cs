using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Infrastructure.Configuration;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// "Link a phone": lets a phone get its own passkey for an account by scanning a QR code
/// shown on a computer that is already signed in.
/// <para>
/// 1. The computer calls <see cref="Create"/> and shows the link URL as a QR code. The URL carries a
///    one-time secret in its fragment, so it never reaches server logs.<br/>
/// 2. The first phone to open it calls <see cref="Claim"/>, receives its own secret and a two-digit
///    number, and displays the number. Later scans are refused.<br/>
/// 3. The computer shows which device scanned plus three numbers; the user picks the one on the phone
///    (<see cref="Confirm"/>). A wrong pick cancels the link, so a photographed QR code is useless.<br/>
/// 4. Only then may the phone create a passkey (challenge purpose <see cref="RegistrationPurpose"/>).
///    <see cref="Complete"/> closes the session and the phone is signed in.
/// </para>
/// Sessions are short-lived and kept in memory (like WebAuthn challenges), so this assumes a
/// single API instance.
/// </summary>
public class DeviceLinkService
{
    /// <summary>Time the phone gets to create its passkey after the number is confirmed.</summary>
    private static readonly TimeSpan RegistrationWindow = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<Guid, Session> _sessions = new();
    private readonly SsoSettings _settings;
    private readonly TimeProvider _time;

    public DeviceLinkService(IOptions<SsoSettings> settings, TimeProvider? time = null)
    {
        _settings = settings.Value;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>WebAuthn challenge purpose for the passkey created during this link.</summary>
    public static string RegistrationPurpose(Guid sessionId) => $"device-link:{sessionId:N}";

    private DateTime Now => _time.GetUtcNow().UtcDateTime;

    // ---------------------------------------------------------------- computer

    public (DeviceLinkCreateResponse Response, string LinkSecret) Create(Guid ownerUserId, string ownerEmail)
    {
        RemoveStale();

        // One active link per account: starting a new one cancels the old QR code.
        foreach (var existing in _sessions.Values.Where(s => s.OwnerUserId == ownerUserId))
        {
            lock (existing)
            {
                if (!existing.IsFinal) existing.Status = DeviceLinkStatus.Denied;
            }
        }

        var linkSecret = SsoCrypto.NewSecret();
        var session = new Session
        {
            Id = Guid.NewGuid(),
            OwnerUserId = ownerUserId,
            OwnerEmail = ownerEmail,
            LinkSecretHash = SsoCrypto.Hash(linkSecret),
            Status = DeviceLinkStatus.Pending,
            ExpiresAt = Now.AddSeconds(_settings.QrSessionLifetimeSeconds)
        };
        _sessions[session.Id] = session;

        var response = new DeviceLinkCreateResponse
        {
            SessionId = session.Id,
            LinkUrl = $"{_settings.PortalBaseUrl.TrimEnd('/')}/qr/link/{session.Id}#k={linkSecret}",
            ExpiresAt = session.ExpiresAt,
            PollIntervalSeconds = 2
        };
        return (response, linkSecret);
    }

    public DeviceLinkStatusDto GetForOwner(Guid sessionId, Guid ownerUserId)
    {
        var session = LoadForOwner(sessionId, ownerUserId);
        lock (session)
        {
            ExpireIfNeeded(session);
            return new DeviceLinkStatusDto
            {
                SessionId = session.Id,
                Status = session.Status,
                ExpiresAt = session.ExpiresAt,
                PhoneUserAgent = session.PhoneUserAgent,
                PhoneIp = session.PhoneIp,
                VerificationChoices = session.Status == DeviceLinkStatus.Scanned ? session.Choices.ToList() : new(),
                DeviceName = session.DeviceName
            };
        }
    }

    public void Confirm(Guid sessionId, Guid ownerUserId, string verificationCode)
    {
        var session = LoadForOwner(sessionId, ownerUserId);
        lock (session)
        {
            ExpireIfNeeded(session);
            if (session.Status != DeviceLinkStatus.Scanned)
            {
                throw new SsoException("invalid_state", $"This link is {session.Status.ToLowerInvariant()}", 409);
            }

            if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(session.VerificationCode ?? string.Empty),
                    Encoding.ASCII.GetBytes(verificationCode ?? string.Empty)))
            {
                // The phone that scanned is probably not the user's: shut it out.
                session.Status = DeviceLinkStatus.Denied;
                throw new SsoException("verification_mismatch",
                    "The number did not match the one on the phone. The link was cancelled for your safety.", 400);
            }

            session.Status = DeviceLinkStatus.Confirmed;
            var deadline = Now.Add(RegistrationWindow);
            if (session.ExpiresAt < deadline) session.ExpiresAt = deadline;
        }
    }

    public void Cancel(Guid sessionId, Guid ownerUserId)
    {
        var session = LoadForOwner(sessionId, ownerUserId);
        lock (session)
        {
            if (!session.IsFinal) session.Status = DeviceLinkStatus.Denied;
        }
    }

    // ------------------------------------------------------------------- phone

    public DeviceLinkClaimResponse Claim(Guid sessionId, string linkSecret, string? phoneIp, string? phoneUserAgent)
    {
        if (!_sessions.TryGetValue(sessionId, out var session) || !SsoCrypto.Matches(linkSecret, session.LinkSecretHash))
        {
            throw SsoException.NotFound("Link request");
        }

        lock (session)
        {
            ExpireIfNeeded(session);
            if (session.Status != DeviceLinkStatus.Pending)
            {
                throw new SsoException("already_used", session.Status == DeviceLinkStatus.Expired
                    ? "This QR code has expired. Show a new one on your computer."
                    : "This QR code was already used. Show a new one on your computer.", 409);
            }

            var phoneSecret = SsoCrypto.NewSecret();
            session.PhoneSecretHash = SsoCrypto.Hash(phoneSecret);
            session.VerificationCode = RandomNumberGenerator.GetInt32(10, 100).ToString();
            session.Choices = BuildChoices(session.VerificationCode);
            session.PhoneIp = Truncate(phoneIp, 64);
            session.PhoneUserAgent = Truncate(phoneUserAgent, 512);
            session.Status = DeviceLinkStatus.Scanned;

            return new DeviceLinkClaimResponse
            {
                PhoneSecret = phoneSecret,
                VerificationCode = session.VerificationCode,
                AccountHint = MaskEmail(session.OwnerEmail),
                ExpiresAt = session.ExpiresAt,
                PollIntervalSeconds = 2
            };
        }
    }

    public DeviceLinkPhoneStatusDto GetForPhone(Guid sessionId, string phoneSecret)
    {
        var session = LoadForPhone(sessionId, phoneSecret);
        lock (session)
        {
            ExpireIfNeeded(session);
            return new DeviceLinkPhoneStatusDto { Status = session.Status, ExpiresAt = session.ExpiresAt };
        }
    }

    /// <summary>Returns the account to register the passkey for; only allowed once confirmed.</summary>
    public (Guid UserId, string Email) RequireConfirmed(Guid sessionId, string phoneSecret)
    {
        var session = LoadForPhone(sessionId, phoneSecret);
        lock (session)
        {
            ExpireIfNeeded(session);
            if (session.Status != DeviceLinkStatus.Confirmed)
            {
                throw new SsoException("invalid_state", session.Status == DeviceLinkStatus.Scanned
                    ? "Pick the number on your computer first."
                    : $"This link is {session.Status.ToLowerInvariant()}", 409);
            }

            return (session.OwnerUserId, session.OwnerEmail);
        }
    }

    /// <summary>Marks the link finished after the phone's passkey was saved.</summary>
    public void Complete(Guid sessionId, string phoneSecret, string? deviceName)
    {
        var session = LoadForPhone(sessionId, phoneSecret);
        lock (session)
        {
            if (session.Status != DeviceLinkStatus.Confirmed)
            {
                throw new SsoException("invalid_state", $"This link is {session.Status.ToLowerInvariant()}", 409);
            }

            session.Status = DeviceLinkStatus.Completed;
            session.DeviceName = deviceName;
        }
    }

    public void CancelFromPhone(Guid sessionId, string phoneSecret)
    {
        var session = LoadForPhone(sessionId, phoneSecret);
        lock (session)
        {
            if (!session.IsFinal) session.Status = DeviceLinkStatus.Denied;
        }
    }

    // ----------------------------------------------------------------- helpers

    private Session LoadForOwner(Guid sessionId, Guid ownerUserId) =>
        _sessions.TryGetValue(sessionId, out var session) && session.OwnerUserId == ownerUserId
            ? session
            : throw SsoException.NotFound("Link request");

    private Session LoadForPhone(Guid sessionId, string phoneSecret) =>
        _sessions.TryGetValue(sessionId, out var session) && SsoCrypto.Matches(phoneSecret, session.PhoneSecretHash)
            ? session
            : throw SsoException.NotFound("Link request");

    private void ExpireIfNeeded(Session session)
    {
        if (!session.IsFinal && session.ExpiresAt < Now)
        {
            session.Status = DeviceLinkStatus.Expired;
        }
    }

    private void RemoveStale()
    {
        var cutoff = Now.AddMinutes(-10);
        foreach (var (id, session) in _sessions)
        {
            if (session.ExpiresAt < cutoff) _sessions.TryRemove(id, out _);
        }
    }

    private static string[] BuildChoices(string correct)
    {
        var choices = new HashSet<string> { correct };
        while (choices.Count < 3)
        {
            choices.Add(RandomNumberGenerator.GetInt32(10, 100).ToString());
        }

        return choices.OrderBy(_ => RandomNumberGenerator.GetInt32(int.MaxValue)).ToArray();
    }

    /// <summary>j***@example.com</summary>
    public static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        return at <= 0 ? "***" : $"{email[0]}***{email[at..]}";
    }

    private static string? Truncate(string? value, int max) =>
        value == null ? null : value.Length <= max ? value : value[..max];

    private sealed class Session
    {
        public Guid Id { get; init; }
        public Guid OwnerUserId { get; init; }
        public string OwnerEmail { get; init; } = string.Empty;
        public string LinkSecretHash { get; init; } = string.Empty;
        public string? PhoneSecretHash { get; set; }
        public string? VerificationCode { get; set; }
        public string[] Choices { get; set; } = Array.Empty<string>();
        public string Status { get; set; } = DeviceLinkStatus.Pending;
        public string? PhoneIp { get; set; }
        public string? PhoneUserAgent { get; set; }
        public string? DeviceName { get; set; }
        public DateTime ExpiresAt { get; set; }

        public bool IsFinal => Status is DeviceLinkStatus.Completed or DeviceLinkStatus.Denied or DeviceLinkStatus.Expired;
    }
}
