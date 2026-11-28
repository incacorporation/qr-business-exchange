using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Qrbound.POS.Data;

namespace Qrbound.POS.Services;

/// <summary>Restaurants and their tables.</summary>
public sealed partial class RestaurantService(IDbContextFactory<PosDbContext> dbFactory)
{
    public async Task<List<Restaurant>> ListAsync()
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Restaurants.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
    }

    public async Task<Restaurant?> GetAsync(Guid id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Restaurants.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id);
    }

    public async Task<Restaurant?> GetBySlugAsync(string slug)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var normalized = slug.Trim().ToLowerInvariant();
        return await db.Restaurants.AsNoTracking().FirstOrDefaultAsync(r => r.Slug == normalized);
    }

    public async Task<Restaurant> CreateAsync(string name, string? slug = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new PosException("Give the restaurant a name.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var baseSlug = Slugify(string.IsNullOrWhiteSpace(slug) ? name : slug);
        var candidate = baseSlug;
        for (var n = 2; await db.Restaurants.AnyAsync(r => r.Slug == candidate); n++)
        {
            candidate = $"{baseSlug}-{n}";
        }

        var restaurant = new Restaurant { Name = name.Trim(), Slug = candidate };
        db.Restaurants.Add(restaurant);
        await db.SaveChangesAsync();
        return restaurant;
    }

    public async Task UpdateAsync(Restaurant changes)
    {
        if (string.IsNullOrWhiteSpace(changes.Name)) throw new PosException("Give the restaurant a name.");
        if (changes.TaxRatePercent is < 0 or > 50) throw new PosException("Tax must be between 0% and 50%.");
        if (changes.Currency is not { Length: 3 }) throw new PosException("Currency is a 3-letter code, e.g. USD.");
        if (!IsKnownTimeZone(changes.TimeZoneId)) throw new PosException($"Unknown time zone \"{changes.TimeZoneId}\".");

        await using var db = await dbFactory.CreateDbContextAsync();
        var r = await db.Restaurants.FirstOrDefaultAsync(x => x.Id == changes.Id) ?? throw new PosException("Restaurant not found.");

        var slug = Slugify(changes.Slug);
        if (slug != r.Slug && await db.Restaurants.AnyAsync(x => x.Slug == slug && x.Id != r.Id))
        {
            throw new PosException($"Another restaurant already uses /r/{slug}.");
        }

        r.Name = changes.Name.Trim();
        r.Slug = slug;
        r.Description = Trim(changes.Description);
        r.LogoUrl = Trim(changes.LogoUrl);
        r.Currency = changes.Currency.ToUpperInvariant();
        r.TaxRatePercent = changes.TaxRatePercent;
        r.QrboundBusinessId = changes.QrboundBusinessId;
        r.OnlineOrderingOpen = changes.OnlineOrderingOpen;
        r.PayBeforeKitchen = changes.PayBeforeKitchen;
        r.AllowTakeaway = changes.AllowTakeaway;
        r.TimeZoneId = changes.TimeZoneId.Trim();
        await db.SaveChangesAsync();
    }

    // ---- Tables ----

    public async Task<List<DiningTable>> ListTablesAsync(Guid restaurantId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Tables.AsNoTracking().Where(t => t.RestaurantId == restaurantId)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.Name).ToListAsync();
    }

    /// <summary>The active table behind a QR code, or null.</summary>
    public async Task<DiningTable?> FindTableAsync(Guid restaurantId, string code)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.Tables.AsNoTracking().FirstOrDefaultAsync(t => t.RestaurantId == restaurantId && t.Code == code && t.IsActive);
    }

    /// <summary>Adds tables named "{prefix} {n}" (e.g. Table 1..Table 12), each with its own code.</summary>
    public async Task AddTablesAsync(Guid restaurantId, string prefix, int from, int count)
    {
        if (count is < 1 or > 200) throw new PosException("Add between 1 and 200 tables at a time.");

        await using var db = await dbFactory.CreateDbContextAsync();
        var order = await db.Tables.Where(t => t.RestaurantId == restaurantId).Select(t => (int?)t.SortOrder).MaxAsync() ?? 0;
        for (var i = 0; i < count; i++)
        {
            db.Tables.Add(new DiningTable
            {
                RestaurantId = restaurantId,
                Name = $"{prefix.Trim()} {from + i}".Trim(),
                Code = Tokens.New(10),
                SortOrder = ++order
            });
        }

        await db.SaveChangesAsync();
    }

    public async Task RenameTableAsync(Guid tableId, string name, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new PosException("Give the table a name.");
        await using var db = await dbFactory.CreateDbContextAsync();
        var t = await db.Tables.FirstOrDefaultAsync(x => x.Id == tableId) ?? throw new PosException("Table not found.");
        t.Name = name.Trim();
        t.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    /// <summary>Gives the table a new code, so its old printed QR code stops working.</summary>
    public async Task NewTableCodeAsync(Guid tableId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var t = await db.Tables.FirstOrDefaultAsync(x => x.Id == tableId) ?? throw new PosException("Table not found.");
        t.Code = Tokens.New(10);
        await db.SaveChangesAsync();
    }

    public async Task DeleteTableAsync(Guid tableId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.Tables.Where(t => t.Id == tableId).ExecuteDeleteAsync();
    }

    // ---- helpers ----

    public static string Slugify(string text)
    {
        var slug = NonSlug().Replace(text.Trim().ToLowerInvariant(), "-").Trim('-');
        if (slug.Length > 60) slug = slug[..60].TrimEnd('-');
        return slug.Length == 0 ? "restaurant" : slug;
    }

    /// <summary>Today's date where the restaurant is.</summary>
    public static DateOnly BusinessDate(Restaurant r, DateTime utcNow)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(r.TimeZoneId, out var z) ? z : TimeZoneInfo.Utc;
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utcNow, zone));
    }

    /// <summary>A UTC time shown in the restaurant's time zone.</summary>
    public static DateTime ToLocal(Restaurant r, DateTime utc)
    {
        var zone = TimeZoneInfo.TryFindSystemTimeZoneById(r.TimeZoneId, out var z) ? z : TimeZoneInfo.Utc;
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone);
    }

    private static bool IsKnownTimeZone(string? id) => !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id.Trim(), out _);

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlug();
}
