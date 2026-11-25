using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Works out what a user may do in a business from its membership (BusinessMembers) and its
/// named roles (BusinessRoles):
/// <list type="bullet">
/// <item>Administrator of a business manages that business, like an Admin member; it gives nothing in other businesses.</item>
/// <item>SuperAdmin has full access, like an owner, in every business.</item>
/// </list>
/// </summary>
public static class BusinessAccess
{
    public static Task<bool> IsSuperAdminAsync(ApplicationDbContext db, Guid userId) =>
        db.BusinessRoles.AnyAsync(r => r.UserId == userId && r.RoleName == BusinessRoleNames.SuperAdmin);

    /// <summary>
    /// The user's effective role in the business: Owner, SuperAdmin, Admin, Member, or null for no access.
    /// </summary>
    public static async Task<string?> GetRoleAsync(ApplicationDbContext db, Guid userId, Guid businessId)
    {
        if (!await db.Businesses.AnyAsync(b => b.BusinessId == businessId))
        {
            return null;
        }

        var memberRole = await db.BusinessMembers
            .Where(m => m.BusinessId == businessId && m.UserId == userId)
            .Select(m => m.Role)
            .FirstOrDefaultAsync();

        var isAdministrator = await db.BusinessRoles.AnyAsync(r =>
            r.BusinessId == businessId && r.UserId == userId && r.RoleName == BusinessRoleNames.Administrator);

        return Combine(memberRole, isAdministrator, await IsSuperAdminAsync(db, userId));
    }

    public static string? Combine(string? memberRole, bool isAdministrator, bool isSuperAdmin)
    {
        if (memberRole == BusinessRoles.Owner)
        {
            return BusinessRoles.Owner;
        }
        if (isSuperAdmin)
        {
            return BusinessRoles.SuperAdmin;
        }
        if (isAdministrator || memberRole == BusinessRoles.Admin)
        {
            return BusinessRoles.Admin;
        }
        return memberRole;
    }

    /// <summary>Ids of the businesses the user manages: Owner or Admin member, or Administrator.</summary>
    public static IQueryable<Guid> ManagedBusinessIds(ApplicationDbContext db, Guid userId) =>
        db.BusinessMembers
            .Where(m => m.UserId == userId && (m.Role == BusinessRoles.Owner || m.Role == BusinessRoles.Admin))
            .Select(m => m.BusinessId)
            .Union(db.BusinessRoles
                .Where(r => r.UserId == userId && r.RoleName == BusinessRoleNames.Administrator)
                .Select(r => r.BusinessId));

    /// <summary>Ids of the users who manage the business: Owner or Admin members, and Administrators.</summary>
    public static IQueryable<Guid> ManagerUserIds(ApplicationDbContext db, Guid businessId) =>
        db.BusinessMembers
            .Where(m => m.BusinessId == businessId && (m.Role == BusinessRoles.Owner || m.Role == BusinessRoles.Admin))
            .Select(m => m.UserId)
            .Union(db.BusinessRoles
                .Where(r => r.BusinessId == businessId && r.RoleName == BusinessRoleNames.Administrator)
                .Select(r => r.UserId));
}
