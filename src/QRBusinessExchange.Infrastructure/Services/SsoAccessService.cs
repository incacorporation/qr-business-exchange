using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Application categories and the users who hold them. One qrbound user can hold different
/// categories in different applications, e.g. "business" (acting for their own restaurant) on
/// eatinhouston.com and "client" on another site.
/// <para>
/// A user requests a category when registering or signing in; categories that require approval
/// stay pending until an Owner or Admin of the application's business decides, and both sides
/// are emailed. Approved categories become <c>category</c> claims in the application's tokens.
/// </para>
/// </summary>
public class SsoAccessService
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailService _email;
    private readonly SsoSettings _settings;
    private readonly ILogger<SsoAccessService> _logger;

    public SsoAccessService(ApplicationDbContext db, IEmailService email, IOptions<SsoSettings> settings,
        ILogger<SsoAccessService> logger)
    {
        _db = db;
        _email = email;
        _settings = settings.Value;
        _logger = logger;
    }

    /// <summary>Errors that mean "signed in, but not allowed into this application yet".</summary>
    public static class Errors
    {
        /// <summary>A request is waiting for the application's administrators.</summary>
        public const string Pending = "access_pending";

        /// <summary>The user must request a category first.</summary>
        public const string Required = "access_required";

        /// <summary>The request was rejected or the access revoked.</summary>
        public const string Denied = "access_denied";
    }

    // -----------------------------------------------------------------------
    // Signed-in user
    // -----------------------------------------------------------------------

    /// <summary>Where <paramref name="userId"/> stands with one category of an application.</summary>
    public async Task<SsoCategoryAccessDto> GetCategoryAccessAsync(Guid userId, string clientId, string categoryKey)
    {
        var app = await GetActiveAppAsync(clientId);
        var category = await GetCategoryAsync(app, categoryKey);
        return await BuildCategoryAccessAsync(userId, app, category);
    }

    /// <summary>Same as <see cref="GetCategoryAccessAsync"/> for a known application; null when nothing was requested.</summary>
    public async Task<SsoCategoryAccessDto?> TryGetCategoryAccessAsync(Guid userId, SsoApplication app, string? categoryKey)
    {
        if (string.IsNullOrEmpty(categoryKey))
        {
            return null;
        }

        var category = await _db.SsoAppCategories.FirstOrDefaultAsync(c =>
            c.SsoApplicationId == app.SsoApplicationId && c.Key == categoryKey);
        return category == null ? null : await BuildCategoryAccessAsync(userId, app, category);
    }

    /// <summary>
    /// Requests a category. Categories that don't require approval are granted at once; the others
    /// stay pending and the application's Owners and Admins are emailed.
    /// </summary>
    public async Task<SsoCategoryAccessDto> RequestAsync(Guid userId, SsoAccessRequest request)
    {
        var app = await GetActiveAppAsync(request.ClientId);
        var category = await GetCategoryAsync(app, request.Category);
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId && u.IsActive)
            ?? throw new SsoException("access_denied", "Account is deactivated", 403);

        Business? actingBusiness = null;
        if (category.RequiresBusiness)
        {
            if (request.BusinessId is not { } businessId)
            {
                throw new SsoException("invalid_request", $"Choose which of your businesses you sign in to {app.Name} for");
            }

            actingBusiness = await GetManagedBusinessAsync(userId, businessId)
                ?? throw new SsoException("invalid_request", "You can only act for a business where you are an Owner or Admin");
        }

        var access = await _db.SsoAppAccess.FirstOrDefaultAsync(a =>
            a.SsoAppCategoryId == category.SsoAppCategoryId && a.UserId == userId);

        if (access?.Status == SsoAccessStatus.Approved && access.BusinessId == actingBusiness?.BusinessId)
        {
            return await BuildCategoryAccessAsync(userId, app, category); // nothing to do
        }

        if (access?.Status == SsoAccessStatus.Pending && access.BusinessId == actingBusiness?.BusinessId)
        {
            return await BuildCategoryAccessAsync(userId, app, category); // already waiting; don't email twice
        }

        var autoApprove = !category.RequiresApproval;
        if (access == null)
        {
            access = new SsoAppAccess
            {
                SsoApplicationId = app.SsoApplicationId,
                SsoAppCategoryId = category.SsoAppCategoryId,
                UserId = userId
            };
            _db.SsoAppAccess.Add(access);
        }

        // A new request (also after a rejection, a revocation or for another business)
        access.BusinessId = actingBusiness?.BusinessId;
        access.Status = autoApprove ? SsoAccessStatus.Approved : SsoAccessStatus.Pending;
        access.RequestNote = Trim(request.Note);
        access.RequestedAt = DateTime.UtcNow;
        access.DecisionNote = null;
        access.DecidedAt = autoApprove ? DateTime.UtcNow : null;
        access.DecidedByUserId = null;

        await SaveAsync();

        if (!autoApprove)
        {
            await NotifyApproversAsync(app, category, user, actingBusiness, access.RequestNote);
        }

        _logger.LogInformation("User {UserId} requested category {Category} of {ClientId}: {Status}",
            userId, category.Key, app.ClientId, access.Status);

        return await BuildCategoryAccessAsync(userId, app, category);
    }

    /// <summary>
    /// Decides whether <paramref name="userId"/> may sign in to <paramref name="app"/> right now and
    /// throws <see cref="Errors"/> codes when not. Grants default and approval-free categories on the way.
    /// </summary>
    public async Task EnsureCanSignInAsync(SsoApplication app, Guid userId, string? requestedCategory)
    {
        var categories = await _db.SsoAppCategories
            .Where(c => c.SsoApplicationId == app.SsoApplicationId)
            .ToListAsync();

        if (categories.Count == 0)
        {
            if (!string.IsNullOrEmpty(requestedCategory))
            {
                throw new SsoException("invalid_request", $"{app.Name} has no category \"{requestedCategory}\"");
            }
            return; // the application doesn't use categories
        }

        var accesses = await _db.SsoAppAccess
            .Where(a => a.SsoApplicationId == app.SsoApplicationId && a.UserId == userId)
            .ToListAsync();

        if (!string.IsNullOrEmpty(requestedCategory))
        {
            var category = categories.FirstOrDefault(c => c.Key == requestedCategory)
                ?? throw new SsoException("invalid_request", $"{app.Name} has no category \"{requestedCategory}\"");

            var access = accesses.FirstOrDefault(a => a.SsoAppCategoryId == category.SsoAppCategoryId);
            switch (access?.Status)
            {
                case SsoAccessStatus.Approved when await IsStillValidAsync(category, access):
                    return;
                case SsoAccessStatus.Pending:
                    throw new SsoException(Errors.Pending,
                        $"Your request to use {app.Name} as {category.Name} is waiting for approval. We'll email you when it's decided.", 403);
                case SsoAccessStatus.Rejected:
                case SsoAccessStatus.Revoked:
                    throw new SsoException(Errors.Denied,
                        $"Your access to {app.Name} as {category.Name} was {access.Status.ToLowerInvariant()}." +
                        (string.IsNullOrEmpty(access.DecisionNote) ? "" : $" {access.DecisionNote}"), 403);
            }

            if (!category.RequiresApproval && !category.RequiresBusiness)
            {
                await GrantAsync(app, category, userId);
                return;
            }

            throw new SsoException(Errors.Required,
                $"Request access to {app.Name} as {category.Name} on your phone first.", 403);
        }

        // No category asked for: hand out default categories, then apply the app's rule.
        foreach (var category in categories.Where(c => c.IsDefault && !c.RequiresApproval && !c.RequiresBusiness))
        {
            if (accesses.All(a => a.SsoAppCategoryId != category.SsoAppCategoryId))
            {
                accesses.Add(await GrantAsync(app, category, userId));
            }
        }

        if (!app.RequireApprovedCategory)
        {
            return;
        }

        foreach (var access in accesses.Where(a => a.Status == SsoAccessStatus.Approved))
        {
            if (await IsStillValidAsync(categories.First(c => c.SsoAppCategoryId == access.SsoAppCategoryId), access))
            {
                return;
            }
        }

        if (accesses.Any(a => a.Status == SsoAccessStatus.Pending))
        {
            throw new SsoException(Errors.Pending,
                $"Your request to use {app.Name} is waiting for approval. We'll email you when it's decided.", 403);
        }

        throw new SsoException(Errors.Required, $"You need approved access to use {app.Name}. Register with it first.", 403);
    }

    /// <summary>The user's approved categories (in the app's order) and the business they act for, for token claims.</summary>
    public async Task<(List<string> Categories, Business? ActingFor)> GetClaimsAsync(SsoApplication app, Guid userId)
    {
        var approved = await _db.SsoAppAccess
            .Include(a => a.Category)
            .Include(a => a.Business)
            .Where(a => a.SsoApplicationId == app.SsoApplicationId && a.UserId == userId && a.Status == SsoAccessStatus.Approved)
            .OrderBy(a => a.Category.SortOrder).ThenBy(a => a.Category.Key)
            .ToListAsync();

        var keys = new List<string>();
        Business? actingFor = null;
        foreach (var access in approved)
        {
            if (!await IsStillValidAsync(access.Category, access))
            {
                continue;
            }

            keys.Add(access.Category.Key);
            actingFor ??= access.Business;
        }

        return (keys, actingFor);
    }

    // -----------------------------------------------------------------------
    // Application administrators (Owner / Admin of the app's business)
    // -----------------------------------------------------------------------

    public async Task<List<SsoAppCategoryDto>> GetCategoriesAsync(Guid actingUserId, Guid businessId, Guid applicationId)
    {
        await RequireMemberAsync(actingUserId, businessId);
        await GetOwnedAppAsync(businessId, applicationId);

        return await _db.SsoAppCategories
            .Where(c => c.SsoApplicationId == applicationId)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Key)
            .Select(c => ToDto(c))
            .ToListAsync();
    }

    public async Task<SsoAppCategoryDto> SaveCategoryAsync(Guid actingUserId, Guid businessId, Guid applicationId,
        Guid? categoryId, SaveSsoAppCategoryRequest request)
    {
        await RequireManagerAsync(actingUserId, businessId);
        await GetOwnedAppAsync(businessId, applicationId);

        var key = request.Key.Trim().ToLowerInvariant();
        if (request.IsDefault && (request.RequiresApproval || request.RequiresBusiness))
        {
            throw new SsoException("invalid_request", "A default category is granted automatically, so it can't require approval or a business");
        }

        if (await _db.SsoAppCategories.AnyAsync(c =>
                c.SsoApplicationId == applicationId && c.Key == key && c.SsoAppCategoryId != categoryId))
        {
            throw new SsoException("duplicate_category", $"This application already has a category \"{key}\"", 409);
        }

        SsoAppCategory category;
        if (categoryId is { } id)
        {
            category = await _db.SsoAppCategories.FirstOrDefaultAsync(c =>
                c.SsoAppCategoryId == id && c.SsoApplicationId == applicationId) ?? throw SsoException.NotFound("Category");
        }
        else
        {
            category = new SsoAppCategory { SsoApplicationId = applicationId, CreatedAt = DateTime.UtcNow };
            _db.SsoAppCategories.Add(category);
        }

        category.Key = key;
        category.Name = request.Name.Trim();
        category.Description = Trim(request.Description);
        category.RequiresApproval = request.RequiresApproval;
        category.IsDefault = request.IsDefault;
        category.RequiresBusiness = request.RequiresBusiness;
        category.SortOrder = request.SortOrder;
        await SaveAsync();

        return ToDto(category);
    }

    /// <summary>Deletes a category and everyone's access to it.</summary>
    public async Task DeleteCategoryAsync(Guid actingUserId, Guid businessId, Guid applicationId, Guid categoryId)
    {
        await RequireManagerAsync(actingUserId, businessId);
        await GetOwnedAppAsync(businessId, applicationId);

        var category = await _db.SsoAppCategories.FirstOrDefaultAsync(c =>
            c.SsoAppCategoryId == categoryId && c.SsoApplicationId == applicationId) ?? throw SsoException.NotFound("Category");
        _db.SsoAppCategories.Remove(category); // access rows cascade
        await SaveAsync();
    }

    public async Task<List<SsoAppAccessDto>> GetAccessAsync(Guid actingUserId, Guid businessId, Guid applicationId, string? status)
    {
        await RequireMemberAsync(actingUserId, businessId);
        await GetOwnedAppAsync(businessId, applicationId);

        var query = _db.SsoAppAccess.Where(a => a.SsoApplicationId == applicationId);
        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(a => a.Status == status);
        }

        return await query
            .OrderBy(a => a.Status == SsoAccessStatus.Pending ? 0 : 1)
            .ThenByDescending(a => a.RequestedAt)
            .Take(500)
            .Select(a => new SsoAppAccessDto
            {
                SsoAppAccessId = a.SsoAppAccessId,
                UserId = a.UserId,
                UserEmail = a.User.Email,
                UserName = (a.User.FirstName + " " + a.User.LastName).Trim(),
                CategoryKey = a.Category.Key,
                CategoryName = a.Category.Name,
                BusinessId = a.BusinessId,
                BusinessName = a.Business != null ? a.Business.Name : null,
                Status = a.Status,
                RequestNote = a.RequestNote,
                DecisionNote = a.DecisionNote,
                RequestedAt = a.RequestedAt,
                DecidedAt = a.DecidedAt
            })
            .ToListAsync();
    }

    /// <summary>Approves, rejects or revokes an access request and emails the user.</summary>
    public async Task<SsoAppAccessDto> DecideAsync(Guid actingUserId, Guid businessId, Guid applicationId, Guid accessId,
        string newStatus, string? note)
    {
        await RequireManagerAsync(actingUserId, businessId);
        var app = await GetOwnedAppAsync(businessId, applicationId);

        var access = await _db.SsoAppAccess
            .Include(a => a.Category)
            .Include(a => a.User)
            .Include(a => a.Business)
            .FirstOrDefaultAsync(a => a.SsoAppAccessId == accessId && a.SsoApplicationId == applicationId)
            ?? throw SsoException.NotFound("Access request");

        var allowed = newStatus switch
        {
            SsoAccessStatus.Approved => access.Status is SsoAccessStatus.Pending or SsoAccessStatus.Rejected or SsoAccessStatus.Revoked,
            SsoAccessStatus.Rejected => access.Status == SsoAccessStatus.Pending,
            SsoAccessStatus.Revoked => access.Status == SsoAccessStatus.Approved,
            _ => false
        };

        if (!allowed)
        {
            throw new SsoException("invalid_state", $"A {access.Status.ToLowerInvariant()} request can't be {newStatus.ToLowerInvariant()}", 409);
        }

        access.Status = newStatus;
        access.DecisionNote = Trim(note);
        access.DecidedAt = DateTime.UtcNow;
        access.DecidedByUserId = actingUserId;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new SsoException("invalid_state", "Someone else decided this request first. Reload to see it.", 409);
        }

        await _email.SendSsoAccessDecisionEmailAsync(
            access.User.Email, access.User.FirstName, app.Name, access.Category.Name,
            newStatus, access.DecisionNote, AppHomeUrl(app));

        _logger.LogInformation("User {ActingUserId} set access {AccessId} ({Category} of {ClientId}) to {Status}",
            actingUserId, accessId, access.Category.Key, app.ClientId, newStatus);

        return new SsoAppAccessDto
        {
            SsoAppAccessId = access.SsoAppAccessId,
            UserId = access.UserId,
            UserEmail = access.User.Email,
            UserName = $"{access.User.FirstName} {access.User.LastName}".Trim(),
            CategoryKey = access.Category.Key,
            CategoryName = access.Category.Name,
            BusinessId = access.BusinessId,
            BusinessName = access.Business?.Name,
            Status = access.Status,
            RequestNote = access.RequestNote,
            DecisionNote = access.DecisionNote,
            RequestedAt = access.RequestedAt,
            DecidedAt = access.DecidedAt
        };
    }

    // -----------------------------------------------------------------------

    private async Task<SsoCategoryAccessDto> BuildCategoryAccessAsync(Guid userId, SsoApplication app, SsoAppCategory category)
    {
        var access = await _db.SsoAppAccess.Include(a => a.Business)
            .FirstOrDefaultAsync(a => a.SsoAppCategoryId == category.SsoAppCategoryId && a.UserId == userId);

        var dto = new SsoCategoryAccessDto
        {
            ClientId = app.ClientId,
            ApplicationName = app.Name,
            CategoryKey = category.Key,
            CategoryName = category.Name,
            Description = category.Description,
            RequiresApproval = category.RequiresApproval,
            RequiresBusiness = category.RequiresBusiness,
            Status = access?.Status,
            BusinessId = access?.BusinessId,
            BusinessName = access?.Business?.Name,
            DecisionNote = access?.DecisionNote
        };

        if (category.RequiresBusiness)
        {
            dto.EligibleBusinesses = await _db.BusinessMembers
                .Where(m => m.UserId == userId && m.Business.IsActive &&
                            (m.Role == BusinessRoles.Owner || m.Role == BusinessRoles.Admin))
                .OrderBy(m => m.Business.Name)
                .Select(m => new SsoEligibleBusinessDto { BusinessId = m.BusinessId, Name = m.Business.Name, Role = m.Role })
                .ToListAsync();
        }

        return dto;
    }

    private async Task<SsoAppAccess> GrantAsync(SsoApplication app, SsoAppCategory category, Guid userId)
    {
        var access = new SsoAppAccess
        {
            SsoApplicationId = app.SsoApplicationId,
            SsoAppCategoryId = category.SsoAppCategoryId,
            UserId = userId,
            Status = SsoAccessStatus.Approved,
            RequestedAt = DateTime.UtcNow,
            DecidedAt = DateTime.UtcNow
        };
        _db.SsoAppAccess.Add(access);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Granted by a parallel sign-in a moment ago
            _db.Entry(access).State = EntityState.Detached;
            return await _db.SsoAppAccess.FirstAsync(a => a.SsoAppCategoryId == category.SsoAppCategoryId && a.UserId == userId);
        }

        return access;
    }

    /// <summary>An approval made for acting as a business only counts while the user still manages it.</summary>
    private async Task<bool> IsStillValidAsync(SsoAppCategory category, SsoAppAccess access)
    {
        if (!category.RequiresBusiness)
        {
            return true;
        }

        return access.BusinessId is { } businessId && await GetManagedBusinessAsync(access.UserId, businessId) != null;
    }

    private async Task<Business?> GetManagedBusinessAsync(Guid userId, Guid businessId) =>
        await _db.BusinessMembers
            .Where(m => m.UserId == userId && m.BusinessId == businessId && m.Business.IsActive &&
                        (m.Role == BusinessRoles.Owner || m.Role == BusinessRoles.Admin))
            .Select(m => m.Business)
            .FirstOrDefaultAsync();

    private async Task NotifyApproversAsync(SsoApplication app, SsoAppCategory category, User requester,
        Business? actingBusiness, string? note)
    {
        var approvers = await _db.BusinessMembers
            .Where(m => m.BusinessId == app.BusinessId &&
                        (m.Role == BusinessRoles.Owner || m.Role == BusinessRoles.Admin) && m.User.IsActive)
            .Select(m => new { m.User.Email, m.User.FirstName })
            .ToListAsync();

        var reviewUrl = $"{_settings.PortalBaseUrl.TrimEnd('/')}/businesses/{app.BusinessId}/applications/{app.SsoApplicationId}/access";
        var requesterName = $"{requester.FirstName} {requester.LastName}".Trim();

        foreach (var approver in approvers)
        {
            await _email.SendSsoAccessRequestedEmailAsync(approver.Email, approver.FirstName, requesterName,
                requester.Email, app.Name, category.Name, actingBusiness?.Name, note, reviewUrl);
        }
    }

    /// <summary>Where users of the app go after approval: the origin of its first web redirect URI.</summary>
    private static string? AppHomeUrl(SsoApplication app) =>
        app.GetRedirectUris()
            .Select(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) ? uri : null)
            .FirstOrDefault(u => u != null && (u.Scheme == Uri.UriSchemeHttps || u.Scheme == Uri.UriSchemeHttp))
            ?.GetLeftPart(UriPartial.Authority);

    private async Task<SsoApplication> GetActiveAppAsync(string clientId)
    {
        var app = await _db.SsoApplications.Include(a => a.Business)
            .FirstOrDefaultAsync(a => a.ClientId == clientId);
        if (app == null || !app.IsActive || !app.Business.IsActive)
        {
            throw new SsoException("invalid_client", "Unknown or disabled application");
        }
        return app;
    }

    private async Task<SsoAppCategory> GetCategoryAsync(SsoApplication app, string categoryKey) =>
        await _db.SsoAppCategories.FirstOrDefaultAsync(c =>
            c.SsoApplicationId == app.SsoApplicationId && c.Key == categoryKey)
        ?? throw new SsoException("invalid_request", $"{app.Name} has no category \"{categoryKey}\"");

    private async Task<SsoApplication> GetOwnedAppAsync(Guid businessId, Guid applicationId) =>
        await _db.SsoApplications.Include(a => a.Business)
            .FirstOrDefaultAsync(a => a.SsoApplicationId == applicationId && a.BusinessId == businessId)
        ?? throw SsoException.NotFound("Application");

    private async Task<string> RequireMemberAsync(Guid userId, Guid businessId) =>
        await _db.BusinessMembers
            .Where(m => m.BusinessId == businessId && m.UserId == userId)
            .Select(m => m.Role)
            .FirstOrDefaultAsync()
        ?? throw SsoException.NotFound("Business");

    private async Task RequireManagerAsync(Guid userId, Guid businessId)
    {
        if (!BusinessRoles.CanManage(await RequireMemberAsync(userId, businessId)))
        {
            throw SsoException.Forbidden();
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new SsoException("invalid_state", "This request changed at the same time. Try again.", 409);
        }
    }

    private static string? Trim(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static SsoAppCategoryDto ToDto(SsoAppCategory c) => new()
    {
        SsoAppCategoryId = c.SsoAppCategoryId,
        Key = c.Key,
        Name = c.Name,
        Description = c.Description,
        RequiresApproval = c.RequiresApproval,
        IsDefault = c.IsDefault,
        RequiresBusiness = c.RequiresBusiness,
        SortOrder = c.SortOrder
    };
}
