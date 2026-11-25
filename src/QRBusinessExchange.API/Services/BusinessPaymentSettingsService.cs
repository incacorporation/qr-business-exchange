using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qrbound.Payments.Models;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Data;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Services;

/// <summary>The settings a payment is checked against, with the business's limits inside the platform's.</summary>
public sealed record EffectivePaymentSettings(
    bool PaymentsEnabled,
    long MinAmountCents,
    long MaxAmountCents,
    int DefaultExpiresInMinutes,
    bool AllowSavedCards,
    bool ManagersOnly)
{
    /// <summary>Whether someone with this role in the business may take payments.</summary>
    public bool CanTakePayments(string? role) =>
        role != null && (!ManagersOnly || BusinessRoles.CanManage(role));
}

/// <summary>
/// Each business's own payment settings and its payments report. Members can read the settings;
/// only Owners, Admins (and SuperAdmins) can change them or see the report.
/// </summary>
public sealed class BusinessPaymentSettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly CardPointeOptions _platform;
    private readonly TimeProvider _clock;
    private readonly ILogger<BusinessPaymentSettingsService> _logger;

    public BusinessPaymentSettingsService(
        ApplicationDbContext db,
        IOptions<CardPointeOptions> platform,
        TimeProvider clock,
        ILogger<BusinessPaymentSettingsService> logger)
    {
        _db = db;
        _platform = platform.Value;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    /// <summary>The settings in force for a business; payments are off when it has never set any.</summary>
    public async Task<EffectivePaymentSettings> GetEffectiveAsync(Guid businessId)
    {
        var s = await _db.BusinessPaymentSettings.AsNoTracking().FirstOrDefaultAsync(x => x.BusinessId == businessId);
        if (s == null)
        {
            return new EffectivePaymentSettings(false, _platform.MinAmountCents, _platform.MaxAmountCents, 30, true, false);
        }

        return new EffectivePaymentSettings(
            s.PaymentsEnabled,
            Math.Max(_platform.MinAmountCents, s.MinAmountCents ?? 0),
            Math.Min(_platform.MaxAmountCents, s.MaxAmountCents ?? long.MaxValue),
            Math.Clamp(s.DefaultExpiresInMinutes, 5, 1440),
            s.AllowSavedCards,
            s.ManagersOnly);
    }

    public async Task<BusinessPaymentSettingsDto> GetAsync(Guid userId, Guid businessId)
    {
        var role = await RequireMemberAsync(userId, businessId);
        var stored = await _db.BusinessPaymentSettings.AsNoTracking().FirstOrDefaultAsync(x => x.BusinessId == businessId);
        return ToDto(businessId, stored, await GetEffectiveAsync(businessId), role);
    }

    public async Task<BusinessPaymentSettingsDto> UpdateAsync(Guid userId, Guid businessId, UpdateBusinessPaymentSettingsRequest request)
    {
        var role = await RequireManagerAsync(userId, businessId);

        if (request.MinAmountCents is { } min && (min < _platform.MinAmountCents || min > _platform.MaxAmountCents))
        {
            throw new QrPaymentException(400, $"The minimum must be between {Money(_platform.MinAmountCents)} and {Money(_platform.MaxAmountCents)}.");
        }

        if (request.MaxAmountCents is { } max && (max < _platform.MinAmountCents || max > _platform.MaxAmountCents))
        {
            throw new QrPaymentException(400, $"The maximum must be between {Money(_platform.MinAmountCents)} and {Money(_platform.MaxAmountCents)}.");
        }

        if (request.MinAmountCents is { } lo && request.MaxAmountCents is { } hi && lo > hi)
        {
            throw new QrPaymentException(400, "The minimum can't be more than the maximum.");
        }

        if (request.DefaultExpiresInMinutes is < 5 or > 1440)
        {
            throw new QrPaymentException(400, "QR codes must work for 5 minutes to 24 hours.");
        }

        var settings = await _db.BusinessPaymentSettings.FirstOrDefaultAsync(x => x.BusinessId == businessId);
        if (settings == null)
        {
            settings = new BusinessPaymentSettings { BusinessId = businessId };
            _db.BusinessPaymentSettings.Add(settings);
        }

        settings.PaymentsEnabled = request.PaymentsEnabled;
        settings.MinAmountCents = request.MinAmountCents;
        settings.MaxAmountCents = request.MaxAmountCents;
        settings.DefaultExpiresInMinutes = request.DefaultExpiresInMinutes;
        settings.AllowSavedCards = request.AllowSavedCards;
        settings.ManagersOnly = request.ManagersOnly;
        settings.UpdatedAt = Now;
        settings.UpdatedByUserId = userId;
        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "Business {BusinessId} payment settings changed by {UserId}: enabled {Enabled}, limits {Min}-{Max}, expiry {Expiry} min, saved cards {Saved}, managers only {ManagersOnly}",
            businessId, userId, settings.PaymentsEnabled, settings.MinAmountCents, settings.MaxAmountCents,
            settings.DefaultExpiresInMinutes, settings.AllowSavedCards, settings.ManagersOnly);

        return ToDto(businessId, settings, await GetEffectiveAsync(businessId), role);
    }

    /// <summary>Totals of the business's QR payments created between two UTC dates (inclusive).</summary>
    public async Task<QrPaymentReportDto> ReportAsync(Guid userId, Guid businessId, DateTime? from, DateTime? to)
    {
        await RequireManagerAsync(userId, businessId);

        var last = (to ?? Now).Date;
        var first = (from ?? last.AddDays(-29)).Date;
        if (first > last)
        {
            throw new QrPaymentException(400, "The start date must be on or before the end date.");
        }

        if ((last - first).TotalDays > 366)
        {
            throw new QrPaymentException(400, "Pick a period of at most a year.");
        }

        var payments = await _db.QrPaymentRequests.AsNoTracking()
            .Where(p => p.BusinessId == businessId && p.CreatedAt >= first && p.CreatedAt < last.AddDays(1))
            .Select(p => new { p.Status, p.AmountCents, p.ExpiresAt, p.PaidAt, p.CreatedAt })
            .ToListAsync();

        string StateOf(string status, DateTime expiresAt) =>
            status == QrPaymentStatus.Open && expiresAt <= Now ? QrPaymentStates.Expired : status;

        var paid = payments.Where(p => p.Status == QrPaymentStatus.Paid).ToList();
        return new QrPaymentReportDto
        {
            From = first,
            To = last,
            Currency = _platform.Currency,
            PaidCount = paid.Count,
            PaidTotalCents = paid.Sum(p => p.AmountCents),
            OpenCount = payments.Count(p => StateOf(p.Status, p.ExpiresAt) is QrPaymentStatus.Open or QrPaymentStatus.Processing),
            ExpiredCount = payments.Count(p => StateOf(p.Status, p.ExpiresAt) == QrPaymentStates.Expired),
            CancelledCount = payments.Count(p => p.Status == QrPaymentStatus.Cancelled),
            LockedCount = payments.Count(p => p.Status == QrPaymentStatus.Locked),
            NeedsReviewCount = payments.Count(p => p.Status == QrPaymentStatus.NeedsReview),
            Days = paid
                .GroupBy(p => (p.PaidAt ?? p.CreatedAt).Date)
                .OrderBy(g => g.Key)
                .Select(g => new QrPaymentReportDayDto { Date = g.Key, PaidCount = g.Count(), PaidTotalCents = g.Sum(p => p.AmountCents) })
                .ToList()
        };
    }

    // ---------------------------------------------------------------------------

    internal async Task<string> RequireMemberAsync(Guid userId, Guid businessId) =>
        await BusinessAccess.GetRoleAsync(_db, userId, businessId)
        ?? throw new QrPaymentException(404, "Business not found.");

    internal async Task<string> RequireManagerAsync(Guid userId, Guid businessId)
    {
        var role = await RequireMemberAsync(userId, businessId);
        if (!BusinessRoles.CanManage(role))
        {
            throw new QrPaymentException(403, "Only the business's Owners and Admins can do this.");
        }

        return role;
    }

    private BusinessPaymentSettingsDto ToDto(Guid businessId, BusinessPaymentSettings? s, EffectivePaymentSettings effective, string role) => new()
    {
        BusinessId = businessId,
        PaymentsEnabled = effective.PaymentsEnabled,
        MinAmountCents = s?.MinAmountCents,
        MaxAmountCents = s?.MaxAmountCents,
        DefaultExpiresInMinutes = effective.DefaultExpiresInMinutes,
        AllowSavedCards = effective.AllowSavedCards,
        ManagersOnly = effective.ManagersOnly,
        EffectiveMinAmountCents = effective.MinAmountCents,
        EffectiveMaxAmountCents = effective.MaxAmountCents,
        PlatformMinAmountCents = _platform.MinAmountCents,
        PlatformMaxAmountCents = _platform.MaxAmountCents,
        Currency = _platform.Currency,
        CanEdit = BusinessRoles.CanManage(role),
        CanTakePayments = effective.PaymentsEnabled && effective.CanTakePayments(role),
        UpdatedAt = s?.UpdatedAt
    };

    private string Money(long cents) => $"{cents / 100m:0.00} {_platform.Currency}";
}
