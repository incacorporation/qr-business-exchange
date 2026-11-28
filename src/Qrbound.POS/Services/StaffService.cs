using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qrbound.POS.Data;

namespace Qrbound.POS.Services;

/// <summary>Scheme and policy names the POS signs staff in with.</summary>
public static class QrboundPosDefaults
{
    /// <summary>The cookie scheme for POS staff.</summary>
    public const string AuthenticationScheme = "QrboundPos.Staff";

    /// <summary>Any active staff member: register, kitchen, orders.</summary>
    public const string StaffPolicy = "QrboundPos.Staff";

    /// <summary>Managers and owners: menu, tables, restaurant settings.</summary>
    public const string ManagerPolicy = "QrboundPos.Manager";

    /// <summary>Owners: staff accounts and new restaurants.</summary>
    public const string OwnerPolicy = "QrboundPos.Owner";

    /// <summary>The cookie scheme for customers signed in with qrbound.</summary>
    public const string CustomerAuthenticationScheme = "QrboundPos.Customer";

    /// <summary>
    /// The menu, ordering and paying: a customer signed in with qrbound (when
    /// <c>QrboundPos:CustomerSignIn:Required</c>, the default), otherwise anyone.
    /// </summary>
    public const string CustomerPolicy = "QrboundPos.Customer";
}

/// <summary>POS staff accounts. One set of staff runs every restaurant in the install.</summary>
public sealed class StaffService(IDbContextFactory<PosDbContext> dbFactory, IOptions<QrboundPosOptions> options, TimeProvider clock)
{
    public const int MaxFailedSignIns = 5;
    public static readonly TimeSpan LockoutTime = TimeSpan.FromMinutes(15);
    public const int MinPasswordLength = 8;

    private static readonly PasswordHasher<StaffMember> Hasher = new();

    public async Task<bool> AnyAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Staff.AnyAsync();
    }

    public async Task<List<StaffMember>> ListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Staff.AsNoTracking().OrderByDescending(s => s.IsActive).ThenBy(s => s.DisplayName).ToListAsync();
    }

    /// <summary>Creates the first Owner. Needs the configured setup key, and only works while there are no staff.</summary>
    public async Task<StaffMember> SetupOwnerAsync(string setupKey, string userName, string displayName, string password)
    {
        var expected = options.Value.SetupKey;
        if (string.IsNullOrWhiteSpace(expected))
        {
            throw new PosException("Setup is closed: set QrboundPos:SetupKey (user secrets or an environment variable) and restart.");
        }

        if (!CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(setupKey ?? string.Empty)))
        {
            throw new PosException("That setup key isn't right.");
        }

        if (await AnyAsync()) throw new PosException("Setup is already done. Sign in instead.");
        return await CreateAsync(userName, displayName, password, StaffRole.Owner);
    }

    public async Task<StaffMember> CreateAsync(string userName, string displayName, string password, StaffRole role)
    {
        var name = NormalizeUserName(userName);
        if (name.Length < 2) throw new PosException("The username needs at least 2 letters or digits.");
        if (string.IsNullOrWhiteSpace(displayName)) throw new PosException("Give the staff member a name.");
        CheckPassword(password);

        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.Staff.AnyAsync(s => s.UserName == name)) throw new PosException($"The username \"{name}\" is taken.");

        var staff = new StaffMember { UserName = name, DisplayName = displayName.Trim(), Role = role };
        staff.PasswordHash = Hasher.HashPassword(staff, password);
        db.Staff.Add(staff);
        await db.SaveChangesAsync();
        return staff;
    }

    public async Task UpdateAsync(Guid id, string displayName, StaffRole role, bool isActive, Guid changedBy)
    {
        if (string.IsNullOrWhiteSpace(displayName)) throw new PosException("Give the staff member a name.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Staff.FirstOrDefaultAsync(x => x.Id == id) ?? throw new PosException("Staff member not found.");

        if (id == changedBy && (role != StaffRole.Owner || !isActive))
        {
            throw new PosException("You can't take away your own Owner access. Ask another owner.");
        }

        s.DisplayName = displayName.Trim();
        s.Role = role;
        s.IsActive = isActive;
        if (isActive) { s.FailedSignIns = 0; s.LockedUntil = null; }
        await db.SaveChangesAsync();
    }

    public async Task SetPasswordAsync(Guid id, string password)
    {
        CheckPassword(password);
        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Staff.FirstOrDefaultAsync(x => x.Id == id) ?? throw new PosException("Staff member not found.");
        s.PasswordHash = Hasher.HashPassword(s, password);
        s.FailedSignIns = 0;
        s.LockedUntil = null;
        await db.SaveChangesAsync();
    }

    /// <summary>Checks a username and password; locks the account for a while after repeated failures.</summary>
    public async Task<StaffMember> VerifyAsync(string userName, string password)
    {
        const string wrong = "Wrong username or password.";
        var name = NormalizeUserName(userName);
        var now = clock.GetUtcNow().UtcDateTime;

        await using var db = await dbFactory.CreateDbContextAsync();
        var s = await db.Staff.FirstOrDefaultAsync(x => x.UserName == name);
        if (s is null)
        {
            Hasher.HashPassword(new StaffMember(), password ?? string.Empty); // same work either way
            throw new PosException(wrong);
        }

        if (!s.IsActive) throw new PosException(wrong);
        if (s.LockedUntil > now) throw new PosException("Too many wrong passwords. Try again in a few minutes, or ask a manager to reset it.");

        var result = Hasher.VerifyHashedPassword(s, s.PasswordHash, password ?? string.Empty);
        if (result == PasswordVerificationResult.Failed)
        {
            s.FailedSignIns++;
            if (s.FailedSignIns >= MaxFailedSignIns)
            {
                s.LockedUntil = now.Add(LockoutTime);
                s.FailedSignIns = 0;
            }

            await db.SaveChangesAsync();
            throw new PosException(wrong);
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded) s.PasswordHash = Hasher.HashPassword(s, password!);
        s.FailedSignIns = 0;
        s.LockedUntil = null;
        s.LastSignInAt = now;
        await db.SaveChangesAsync();
        return s;
    }

    /// <summary>The identity put in the staff cookie.</summary>
    public static ClaimsPrincipal ToPrincipal(StaffMember s) =>
        new(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, s.Id.ToString()),
            new Claim(ClaimTypes.Name, s.DisplayName),
            new Claim("pos_username", s.UserName),
            new Claim(ClaimTypes.Role, s.Role.ToString())
        ], QrboundPosDefaults.AuthenticationScheme, ClaimTypes.Name, ClaimTypes.Role));

    /// <summary>The staff member's current role, or null when the account is gone or switched off.</summary>
    internal async Task<StaffRole?> CurrentRoleAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Staff.Where(s => s.Id == id && s.IsActive).Select(s => (StaffRole?)s.Role).FirstOrDefaultAsync();
    }

    private static string NormalizeUserName(string? userName) => (userName ?? string.Empty).Trim().ToLowerInvariant();

    private static void CheckPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            throw new PosException($"The password needs at least {MinPasswordLength} characters.");
        }
    }
}
