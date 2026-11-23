using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// "Scan with your signed-in phone" login.
/// <para>
/// 1. The desktop calls <see cref="CreateAsync"/> and shows the returned URL as a QR code
///    together with a two-digit verification number. It keeps the poll secret.<br/>
/// 2. A phone that is already signed in opens the URL; <see cref="GetForApproverAsync"/> marks it scanned
///    and shows three numbers.<br/>
/// 3. The user picks the number shown on the desktop and confirms with a passkey;
///    <see cref="ApproveAsync"/> binds the session to that user.<br/>
/// 4. The desktop's <see cref="PollAsync"/> sees Approved exactly once and receives a session.
/// </para>
/// </summary>
public class QrLoginService
{
    /// <summary>The WebAuthn challenge purpose used when approving a session.</summary>
    public static string ApprovalPurpose(Guid sessionId) => $"qr-approve:{sessionId:N}";

    private static readonly string[] FinalStatuses =
        { QrLoginStatus.Denied, QrLoginStatus.Consumed, QrLoginStatus.Expired };

    private readonly ApplicationDbContext _db;
    private readonly SsoSettings _settings;
    private readonly SsoAccessService _access;

    public QrLoginService(ApplicationDbContext db, IOptions<SsoSettings> settings, SsoAccessService access)
    {
        _db = db;
        _settings = settings.Value;
        _access = access;
    }

    public async Task<QrLoginCreateResponse> CreateAsync(string? clientId, string? requesterIp, string? userAgent,
        string? redirectUri = null, string? category = null)
    {
        Guid? applicationId = null;
        category = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var app = await _db.SsoApplications.Include(a => a.Business)
                .FirstOrDefaultAsync(a => a.ClientId == clientId);
            if (app == null || !app.IsActive || !app.Business.IsActive)
            {
                throw new SsoException("invalid_client", "Unknown or disabled application");
            }

            // Fail fast: the code will be bound to this redirect URI when it is collected.
            if (!string.IsNullOrEmpty(redirectUri) &&
                !app.GetRedirectUris().Contains(redirectUri, StringComparer.Ordinal))
            {
                throw new SsoException("invalid_redirect_uri", "The redirect URI is not registered for this application");
            }

            if (category != null &&
                !await _db.SsoAppCategories.AnyAsync(c => c.SsoApplicationId == app.SsoApplicationId && c.Key == category))
            {
                throw new SsoException("invalid_request", $"{app.Name} has no category \"{category}\"");
            }

            applicationId = app.SsoApplicationId;
        }
        else if (!string.IsNullOrEmpty(redirectUri))
        {
            throw new SsoException("invalid_request", "redirectUri requires clientId");
        }
        else if (category != null)
        {
            throw new SsoException("invalid_request", "category requires clientId");
        }

        var pollSecret = SsoCrypto.NewSecret();
        var session = new QrLoginSession
        {
            PollSecretHash = SsoCrypto.Hash(pollSecret),
            SsoApplicationId = applicationId,
            RequestedCategory = category,
            Status = QrLoginStatus.Pending,
            VerificationCode = RandomNumberGenerator.GetInt32(10, 100).ToString(),
            RequesterIp = Truncate(requesterIp, 64),
            RequesterUserAgent = Truncate(userAgent, 512),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(_settings.QrSessionLifetimeSeconds)
        };

        _db.QrLoginSessions.Add(session);

        // Opportunistic cleanup
        var cutoff = DateTime.UtcNow.AddDays(-1);
        await _db.QrLoginSessions.Where(s => s.ExpiresAt < cutoff).ExecuteDeleteAsync();

        await _db.SaveChangesAsync();

        return new QrLoginCreateResponse
        {
            SessionId = session.QrLoginSessionId,
            PollSecret = pollSecret,
            ApproveUrl = $"{_settings.PortalBaseUrl.TrimEnd('/')}/qr/approve/{session.QrLoginSessionId}",
            VerificationCode = session.VerificationCode,
            ExpiresAt = session.ExpiresAt,
            PollIntervalSeconds = 2
        };
    }

    /// <summary>
    /// Called by the signed-in phone after scanning. Marks the session as scanned.
    /// </summary>
    public async Task<QrLoginSessionInfoDto> GetForApproverAsync(Guid sessionId, Guid approverUserId)
    {
        var session = await LoadAsync(sessionId);
        await ExpireIfNeededAsync(session);

        if (session.Status == QrLoginStatus.Pending)
        {
            session.Status = QrLoginStatus.Scanned;
            session.ScannedAt = DateTime.UtcNow;
            await TrySaveAsync();
        }

        return new QrLoginSessionInfoDto
        {
            SessionId = session.QrLoginSessionId,
            Status = session.Status,
            ApplicationName = session.Application?.Name,
            BusinessName = session.Application?.Business.Name,
            RequesterIp = session.RequesterIp,
            RequesterUserAgent = session.RequesterUserAgent,
            CreatedAt = session.CreatedAt,
            ExpiresAt = session.ExpiresAt,
            VerificationChoices = BuildChoices(session.QrLoginSessionId, session.VerificationCode),
            CategoryAccess = session.Application == null
                ? null
                : await _access.TryGetCategoryAccessAsync(approverUserId, session.Application, session.RequestedCategory)
        };
    }

    /// <summary>
    /// Approves the session for <paramref name="approverUserId"/>. The caller must already
    /// have verified a passkey assertion that belongs to <paramref name="passkeyUserId"/>.
    /// </summary>
    public async Task ApproveAsync(Guid sessionId, Guid approverUserId, Guid passkeyUserId, string verificationCode)
    {
        if (approverUserId != passkeyUserId)
        {
            throw new SsoException("access_denied", "The passkey does not belong to the signed-in account", 403);
        }

        var session = await LoadAsync(sessionId);
        await ExpireIfNeededAsync(session);

        if (session.Status is not (QrLoginStatus.Pending or QrLoginStatus.Scanned))
        {
            throw new SsoException("invalid_state", $"This sign-in request is {session.Status.ToLowerInvariant()}", 409);
        }

        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(session.VerificationCode),
                Encoding.ASCII.GetBytes(verificationCode ?? string.Empty)))
        {
            // A wrong pick usually means the QR came from somewhere else: kill the request.
            session.Status = QrLoginStatus.Denied;
            session.CompletedAt = DateTime.UtcNow;
            await TrySaveAsync();
            throw new SsoException("verification_mismatch",
                "The number did not match. The sign-in request was blocked for your safety.", 400);
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == approverUserId);
        if (user == null || !user.IsActive)
        {
            throw new SsoException("access_denied", "Account is deactivated", 403);
        }

        session.Status = QrLoginStatus.Approved;
        session.ApprovedUserId = approverUserId;
        session.CompletedAt = DateTime.UtcNow;

        if (!await TrySaveAsync())
        {
            throw new SsoException("invalid_state", "This sign-in request was already handled", 409);
        }
    }

    public async Task DenyAsync(Guid sessionId)
    {
        var session = await LoadAsync(sessionId);
        if (session.Status is QrLoginStatus.Pending or QrLoginStatus.Scanned)
        {
            session.Status = QrLoginStatus.Denied;
            session.CompletedAt = DateTime.UtcNow;
            await TrySaveAsync();
        }
    }

    /// <summary>
    /// Called by the desktop. Returns the approved user id exactly once.
    /// </summary>
    public async Task<(string Status, DateTime ExpiresAt, Guid? ApprovedUserId)> PollAsync(Guid sessionId, string pollSecret)
    {
        var session = await _db.QrLoginSessions.FirstOrDefaultAsync(s => s.QrLoginSessionId == sessionId);
        if (session == null || !SsoCrypto.Matches(pollSecret, session.PollSecretHash))
        {
            throw SsoException.NotFound("Sign-in request");
        }

        await ExpireIfNeededAsync(session);

        if (session.Status == QrLoginStatus.Approved && session.ApprovedUserId.HasValue)
        {
            session.Status = QrLoginStatus.Consumed;
            if (await TrySaveAsync())
            {
                return (QrLoginStatus.Approved, session.ExpiresAt, session.ApprovedUserId);
            }

            return (QrLoginStatus.Consumed, session.ExpiresAt, null);
        }

        return (session.Status, session.ExpiresAt, null);
    }

    /// <summary>
    /// For the requesting (desktop) side: checks the poll secret and returns the client_id the
    /// session was created for (null = the core portal). Does not change the session.
    /// </summary>
    public async Task<string?> GetClientIdForPollerAsync(Guid sessionId, string pollSecret) =>
        (await GetRequestForPollerAsync(sessionId, pollSecret)).ClientId;

    /// <summary>
    /// Like <see cref="GetClientIdForPollerAsync"/>, plus the application category the session asked for.
    /// </summary>
    public async Task<(string? ClientId, string? Category)> GetRequestForPollerAsync(Guid sessionId, string pollSecret)
    {
        var session = await _db.QrLoginSessions
            .Include(s => s.Application)
            .FirstOrDefaultAsync(s => s.QrLoginSessionId == sessionId);

        if (session == null || !SsoCrypto.Matches(pollSecret, session.PollSecretHash))
        {
            throw SsoException.NotFound("Sign-in request");
        }

        return (session.Application?.ClientId, session.RequestedCategory);
    }

    public async Task<string?> GetClientIdAsync(Guid sessionId) =>
        await _db.QrLoginSessions
            .Where(s => s.QrLoginSessionId == sessionId && s.Application != null)
            .Select(s => s.Application!.ClientId)
            .FirstOrDefaultAsync();

    // -----------------------------------------------------------------------

    private async Task<QrLoginSession> LoadAsync(Guid sessionId) =>
        await _db.QrLoginSessions
            .Include(s => s.Application).ThenInclude(a => a!.Business)
            .FirstOrDefaultAsync(s => s.QrLoginSessionId == sessionId)
        ?? throw SsoException.NotFound("Sign-in request");

    private async Task ExpireIfNeededAsync(QrLoginSession session)
    {
        // Allow a short grace period so an approval made at the last second can still be collected
        var deadline = session.Status == QrLoginStatus.Approved ? session.ExpiresAt.AddSeconds(30) : session.ExpiresAt;
        if (deadline < DateTime.UtcNow && !FinalStatuses.Contains(session.Status))
        {
            session.Status = QrLoginStatus.Expired;
            await TrySaveAsync();
        }
    }

    /// <summary>Saves; returns false if another request changed the session first.</summary>
    private async Task<bool> TrySaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            foreach (var entry in ex.Entries)
            {
                await entry.ReloadAsync();
            }
            return false;
        }
    }

    /// <summary>
    /// Three candidate numbers including the correct one. Derived from the session so they are the
    /// same on every reload; random decoys per request would let someone find the real number by
    /// reloading and seeing which number never changes.
    /// </summary>
    public static List<string> BuildChoices(Guid sessionId, string correct)
    {
        var seed = SHA256.HashData(Encoding.UTF8.GetBytes($"{sessionId:N}:{correct}"));
        var choices = new List<string> { correct };
        for (var i = 0; choices.Count < 3 && i < seed.Length; i++)
        {
            var decoy = (10 + seed[i] % 90).ToString();
            if (!choices.Contains(decoy)) choices.Add(decoy);
        }

        // Stable position for the correct number
        var position = seed[^1] % choices.Count;
        (choices[0], choices[position]) = (choices[position], choices[0]);
        return choices;
    }

    private static string? Truncate(string? value, int max) =>
        value == null ? null : value.Length <= max ? value : value[..max];
}
