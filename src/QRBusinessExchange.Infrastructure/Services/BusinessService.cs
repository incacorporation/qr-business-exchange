using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Manages businesses (tenants), their members, and the applications they register for SSO.
/// Every operation is performed on behalf of an acting user and checks that user's role.
/// </summary>
public class BusinessService
{
    private readonly ApplicationDbContext _db;

    public BusinessService(ApplicationDbContext db)
    {
        _db = db;
    }

    // -----------------------------------------------------------------------
    // Businesses
    // -----------------------------------------------------------------------

    public async Task<List<BusinessDto>> GetMyBusinessesAsync(Guid userId)
    {
        return await _db.BusinessMembers
            .Where(m => m.UserId == userId)
            .Select(m => new BusinessDto
            {
                BusinessId = m.Business.BusinessId,
                Name = m.Business.Name,
                Slug = m.Business.Slug,
                Description = m.Business.Description,
                WebsiteUrl = m.Business.WebsiteUrl,
                LogoUrl = m.Business.LogoUrl,
                IsActive = m.Business.IsActive,
                MyRole = m.Role,
                ApplicationCount = m.Business.Applications.Count,
                MemberCount = m.Business.Members.Count,
                CreatedAt = m.Business.CreatedAt
            })
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<BusinessDto> GetBusinessAsync(Guid userId, Guid businessId)
    {
        var dto = (await GetMyBusinessesAsync(userId)).FirstOrDefault(b => b.BusinessId == businessId);
        return dto ?? throw SsoException.NotFound("Business");
    }

    public async Task<BusinessDto> CreateBusinessAsync(Guid userId, CreateBusinessRequest request)
    {
        var slug = Slugify(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug);
        if (slug.Length < 2)
        {
            throw new SsoException("invalid_request", "Business slug must contain at least 2 letters or digits");
        }

        if (await _db.Businesses.AnyAsync(b => b.Slug == slug))
        {
            throw new SsoException("slug_taken", $"The slug '{slug}' is already in use", 409);
        }

        var business = new Business
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Description = request.Description,
            WebsiteUrl = request.WebsiteUrl,
            LogoUrl = request.LogoUrl,
            OwnerUserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        business.Members.Add(new BusinessMember
        {
            UserId = userId,
            Role = BusinessRoles.Owner,
            CreatedAt = DateTime.UtcNow
        });

        _db.Businesses.Add(business);
        await _db.SaveChangesAsync();

        return await GetBusinessAsync(userId, business.BusinessId);
    }

    public async Task<BusinessDto> UpdateBusinessAsync(Guid userId, Guid businessId, UpdateBusinessRequest request)
    {
        await RequireManagerAsync(userId, businessId);

        var business = await _db.Businesses.FirstAsync(b => b.BusinessId == businessId);
        business.Name = request.Name.Trim();
        business.Description = request.Description;
        business.WebsiteUrl = request.WebsiteUrl;
        business.LogoUrl = request.LogoUrl;
        business.IsActive = request.IsActive;
        business.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return await GetBusinessAsync(userId, businessId);
    }

    public async Task DeleteBusinessAsync(Guid userId, Guid businessId)
    {
        var role = await GetRoleAsync(userId, businessId) ?? throw SsoException.NotFound("Business");
        if (role != BusinessRoles.Owner)
        {
            throw SsoException.Forbidden("Only the owner can delete a business");
        }

        var business = await _db.Businesses.FirstAsync(b => b.BusinessId == businessId);
        _db.Businesses.Remove(business);
        await _db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // Members
    // -----------------------------------------------------------------------

    public async Task<List<BusinessMemberDto>> GetMembersAsync(Guid userId, Guid businessId)
    {
        await RequireMemberAsync(userId, businessId);

        return await _db.BusinessMembers
            .Where(m => m.BusinessId == businessId)
            .OrderBy(m => m.User.Email)
            .Select(m => new BusinessMemberDto
            {
                UserId = m.UserId,
                Email = m.User.Email,
                FullName = m.User.FirstName + " " + m.User.LastName,
                Role = m.Role,
                CreatedAt = m.CreatedAt
            })
            .ToListAsync();
    }

    public async Task<BusinessMemberDto> AddOrUpdateMemberAsync(Guid userId, Guid businessId, AddBusinessMemberRequest request)
    {
        var actingRole = await RequireManagerAsync(userId, businessId);

        if (!BusinessRoles.All.Contains(request.Role))
        {
            throw new SsoException("invalid_request", $"Role must be one of: {string.Join(", ", BusinessRoles.All)}");
        }

        if (request.Role == BusinessRoles.Owner && actingRole != BusinessRoles.Owner)
        {
            throw SsoException.Forbidden("Only an owner can grant the Owner role");
        }

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email)
            ?? throw new SsoException("user_not_found", "No account exists with that email. Ask them to register first.", 404);

        var member = await _db.BusinessMembers.FirstOrDefaultAsync(m => m.BusinessId == businessId && m.UserId == user.UserId);
        if (member == null)
        {
            member = new BusinessMember { BusinessId = businessId, UserId = user.UserId, CreatedAt = DateTime.UtcNow };
            _db.BusinessMembers.Add(member);
        }
        else if (member.Role == BusinessRoles.Owner && actingRole != BusinessRoles.Owner)
        {
            throw SsoException.Forbidden("Only an owner can change another owner's role");
        }

        member.Role = request.Role;
        await EnsureOwnerRemainsAsync(businessId, changingUserId: user.UserId, newRole: request.Role);
        await _db.SaveChangesAsync();

        return new BusinessMemberDto
        {
            UserId = user.UserId,
            Email = user.Email,
            FullName = $"{user.FirstName} {user.LastName}",
            Role = member.Role,
            CreatedAt = member.CreatedAt
        };
    }

    public async Task RemoveMemberAsync(Guid userId, Guid businessId, Guid memberUserId)
    {
        var actingRole = await RequireManagerAsync(userId, businessId);

        var member = await _db.BusinessMembers.FirstOrDefaultAsync(m => m.BusinessId == businessId && m.UserId == memberUserId)
            ?? throw SsoException.NotFound("Member");

        if (member.Role == BusinessRoles.Owner && actingRole != BusinessRoles.Owner)
        {
            throw SsoException.Forbidden("Only an owner can remove an owner");
        }

        await EnsureOwnerRemainsAsync(businessId, changingUserId: memberUserId, newRole: null);
        _db.BusinessMembers.Remove(member);
        await _db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // Applications
    // -----------------------------------------------------------------------

    public async Task<List<SsoApplicationDto>> GetApplicationsAsync(Guid userId, Guid businessId)
    {
        await RequireMemberAsync(userId, businessId);

        var apps = await _db.SsoApplications
            .Where(a => a.BusinessId == businessId)
            .OrderBy(a => a.Name)
            .ToListAsync();

        return apps.Select(a => ToDto(a)).ToList();
    }

    public async Task<SsoApplicationDto> CreateApplicationAsync(Guid userId, Guid businessId, CreateSsoApplicationRequest request)
    {
        await RequireManagerAsync(userId, businessId);

        var redirectUris = ValidateRedirectUris(request.RedirectUris);
        string? secret = request.IsConfidential ? SsoCrypto.NewSecret() : null;

        var app = new SsoApplication
        {
            BusinessId = businessId,
            Name = request.Name.Trim(),
            ClientId = "qrb_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(10)).ToLowerInvariant(),
            ClientSecretHash = secret == null ? null : SsoCrypto.Hash(secret),
            RedirectUris = string.Join('\n', redirectUris),
            RequireMembership = request.RequireMembership,
            TokenLifetimeMinutes = request.TokenLifetimeMinutes,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _db.SsoApplications.Add(app);
        await _db.SaveChangesAsync();

        return ToDto(app, secret);
    }

    public async Task<SsoApplicationDto> UpdateApplicationAsync(Guid userId, Guid businessId, Guid applicationId, UpdateSsoApplicationRequest request)
    {
        await RequireManagerAsync(userId, businessId);
        var app = await GetOwnedApplicationAsync(businessId, applicationId);

        app.Name = request.Name.Trim();
        app.RedirectUris = string.Join('\n', ValidateRedirectUris(request.RedirectUris));
        app.RequireMembership = request.RequireMembership;
        app.TokenLifetimeMinutes = request.TokenLifetimeMinutes;
        app.IsActive = request.IsActive;
        app.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ToDto(app);
    }

    public async Task<SsoApplicationDto> RotateSecretAsync(Guid userId, Guid businessId, Guid applicationId)
    {
        await RequireManagerAsync(userId, businessId);
        var app = await GetOwnedApplicationAsync(businessId, applicationId);

        if (!app.IsConfidential)
        {
            throw new SsoException("invalid_request", "Public applications do not use a client secret");
        }

        var secret = SsoCrypto.NewSecret();
        app.ClientSecretHash = SsoCrypto.Hash(secret);
        app.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return ToDto(app, secret);
    }

    public async Task DeleteApplicationAsync(Guid userId, Guid businessId, Guid applicationId)
    {
        await RequireManagerAsync(userId, businessId);
        var app = await GetOwnedApplicationAsync(businessId, applicationId);
        _db.SsoApplications.Remove(app);
        await _db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    public async Task<string?> GetRoleAsync(Guid userId, Guid businessId) =>
        await _db.BusinessMembers
            .Where(m => m.BusinessId == businessId && m.UserId == userId)
            .Select(m => m.Role)
            .FirstOrDefaultAsync();

    private async Task<string> RequireMemberAsync(Guid userId, Guid businessId) =>
        await GetRoleAsync(userId, businessId) ?? throw SsoException.NotFound("Business");

    private async Task<string> RequireManagerAsync(Guid userId, Guid businessId)
    {
        var role = await RequireMemberAsync(userId, businessId);
        if (!BusinessRoles.CanManage(role))
        {
            throw SsoException.Forbidden();
        }
        return role;
    }

    private async Task EnsureOwnerRemainsAsync(Guid businessId, Guid changingUserId, string? newRole)
    {
        if (newRole == BusinessRoles.Owner)
        {
            return;
        }

        var otherOwners = await _db.BusinessMembers.CountAsync(m =>
            m.BusinessId == businessId && m.Role == BusinessRoles.Owner && m.UserId != changingUserId);

        var isOwnerNow = await _db.BusinessMembers.AnyAsync(m =>
            m.BusinessId == businessId && m.UserId == changingUserId && m.Role == BusinessRoles.Owner);

        if (isOwnerNow && otherOwners == 0)
        {
            throw new SsoException("last_owner", "A business must keep at least one owner");
        }
    }

    private async Task<SsoApplication> GetOwnedApplicationAsync(Guid businessId, Guid applicationId) =>
        await _db.SsoApplications.FirstOrDefaultAsync(a => a.SsoApplicationId == applicationId && a.BusinessId == businessId)
        ?? throw SsoException.NotFound("Application");

    private static SsoApplicationDto ToDto(SsoApplication app, string? secret = null) => new()
    {
        SsoApplicationId = app.SsoApplicationId,
        BusinessId = app.BusinessId,
        Name = app.Name,
        ClientId = app.ClientId,
        IsConfidential = app.IsConfidential,
        RedirectUris = app.GetRedirectUris().ToList(),
        RequireMembership = app.RequireMembership,
        TokenLifetimeMinutes = app.TokenLifetimeMinutes,
        IsActive = app.IsActive,
        CreatedAt = app.CreatedAt,
        ClientSecret = secret
    };

    /// <summary>
    /// Redirect URIs must be absolute, have no fragment, and use HTTPS unless they point
    /// at a loopback host. Custom schemes (e.g. "myapp://callback") are allowed for mobile apps.
    /// </summary>
    public static List<string> ValidateRedirectUris(IEnumerable<string> uris)
    {
        var result = new List<string>();
        foreach (var raw in uris.Select(u => u?.Trim()).Where(u => !string.IsNullOrEmpty(u)))
        {
            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                throw new SsoException("invalid_redirect_uri", $"'{raw}' is not an absolute URI");
            }

            if (!string.IsNullOrEmpty(uri.Fragment))
            {
                throw new SsoException("invalid_redirect_uri", $"'{raw}' must not contain a fragment");
            }

            var scheme = uri.Scheme.ToLowerInvariant();
            if (scheme is "javascript" or "data" or "file" or "vbscript")
            {
                throw new SsoException("invalid_redirect_uri", $"'{raw}' uses a forbidden scheme");
            }

            if (scheme == "http" && !uri.IsLoopback)
            {
                throw new SsoException("invalid_redirect_uri", $"'{raw}' must use https (http is only allowed for localhost)");
            }

            result.Add(raw!);
        }

        if (result.Count == 0)
        {
            throw new SsoException("invalid_redirect_uri", "At least one redirect URI is required");
        }

        return result.Distinct(StringComparer.Ordinal).ToList();
    }

    private static string Slugify(string value)
    {
        var slug = Regex.Replace(value.Trim().ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
        return slug.Length > 100 ? slug[..100].Trim('-') : slug;
    }
}
