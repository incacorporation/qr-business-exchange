using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Ping Me and "nearby business professionals". A user who turns Ping Me on shares their location and
/// appears on other users' nearby maps; turning it off hides them straight away. Each user has one
/// UserLocations row, updated whenever their app shares a new position.
/// </summary>
/// <remarks>
/// Queries project only the columns they need and writes go through ExecuteUpdate or SQL: the table's
/// GeographyPoint column is SQL geography but mapped to string, so loading or saving whole entities
/// would fail on it.
/// </remarks>
public class NearbyService
{
    /// <summary>People whose location is older than this are left off nearby maps (they've likely moved on).</summary>
    public static readonly TimeSpan LocationFreshFor = TimeSpan.FromHours(24);

    public const double MaxRadiusMiles = 50;

    /// <summary>Most people returned by one nearby search, nearest first.</summary>
    public const int MaxResults = 200;

    private const double EarthRadiusMiles = 3958.8;

    private readonly ApplicationDbContext _db;
    private readonly TimeProvider _clock;

    public NearbyService(ApplicationDbContext db, TimeProvider? clock = null)
    {
        _db = db;
        _clock = clock ?? TimeProvider.System;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<PingMeStatusDto> GetStatusAsync(Guid userId)
    {
        var row = await _db.UserLocations
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.LastUpdated)
            .Select(l => new { l.PingMeEnabled, l.LastUpdated })
            .FirstOrDefaultAsync();

        return new PingMeStatusDto { Enabled = row?.PingMeEnabled ?? false, LastUpdated = row?.LastUpdated };
    }

    /// <summary>Saves the user's current position, and turns Ping Me on or off when asked to.</summary>
    public async Task<PingMeStatusDto> ShareLocationAsync(Guid userId, double latitude, double longitude, bool? pingMeEnabled)
    {
        if (!IsValid(latitude, longitude))
        {
            throw new SsoException("invalid_location", "That location isn't valid.");
        }

        var lat = Math.Round((decimal)latitude, 8);
        var lng = Math.Round((decimal)longitude, 8);
        var now = Now;

        var ids = await _db.UserLocations
            .Where(l => l.UserId == userId)
            .OrderByDescending(l => l.LastUpdated)
            .Select(l => l.LocationId)
            .ToListAsync();

        if (ids.Count == 0)
        {
            var enabled = pingMeEnabled ?? false;
            await _db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO UserLocations (LocationId, UserId, Latitude, Longitude, PingMeEnabled, LastUpdated) VALUES ({Guid.NewGuid()}, {userId}, {lat}, {lng}, {enabled}, {now})");
        }
        else
        {
            var newest = ids[0];
            if (pingMeEnabled is { } enabled)
            {
                await _db.UserLocations.Where(l => l.LocationId == newest).ExecuteUpdateAsync(s => s
                    .SetProperty(l => l.Latitude, lat)
                    .SetProperty(l => l.Longitude, lng)
                    .SetProperty(l => l.LastUpdated, now)
                    .SetProperty(l => l.PingMeEnabled, enabled));
            }
            else
            {
                await _db.UserLocations.Where(l => l.LocationId == newest).ExecuteUpdateAsync(s => s
                    .SetProperty(l => l.Latitude, lat)
                    .SetProperty(l => l.Longitude, lng)
                    .SetProperty(l => l.LastUpdated, now));
            }

            if (ids.Count > 1)
            {
                // One row per user; older duplicates would otherwise keep showing an old position.
                await _db.UserLocations.Where(l => l.UserId == userId && l.LocationId != newest).ExecuteDeleteAsync();
            }
        }

        return await GetStatusAsync(userId);
    }

    /// <summary>
    /// Turns Ping Me on or off. Turning it on needs a shared location first (ShareLocationAsync with
    /// pingMeEnabled: true does both at once).
    /// </summary>
    public async Task<PingMeStatusDto> SetPingMeAsync(Guid userId, bool enabled)
    {
        var updated = await _db.UserLocations
            .Where(l => l.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.PingMeEnabled, enabled));

        if (updated == 0 && enabled)
        {
            throw new SsoException("location_required",
                "Allow location access so people nearby can find you, then turn Ping Me on again.", 409);
        }

        return await GetStatusAsync(userId);
    }

    /// <summary>
    /// People with Ping Me on within the radius of the given point, nearest first. The searcher is left out,
    /// as are people whose location is older than <see cref="LocationFreshFor"/>. Company and picture follow
    /// each person's "Show on my card" choices; positions are rounded to about 100 m.
    /// </summary>
    public async Task<List<NearbyUserDto>> GetNearbyAsync(Guid userId, double latitude, double longitude, double radiusMiles)
    {
        if (!IsValid(latitude, longitude))
        {
            throw new SsoException("invalid_location", "That location isn't valid.");
        }
        if (radiusMiles <= 0 || radiusMiles > MaxRadiusMiles)
        {
            throw new SsoException("invalid_radius", $"The distance must be between 0 and {MaxRadiusMiles} miles.");
        }

        // A box around the point narrows the search in SQL; the exact distance is checked below.
        var latDelta = radiusMiles / 69.0;
        var lngDelta = radiusMiles / (69.0 * Math.Max(Math.Cos(latitude * Math.PI / 180), 0.01));
        var minLat = (decimal)(latitude - latDelta);
        var maxLat = (decimal)(latitude + latDelta);
        var minLng = (decimal)(longitude - lngDelta);
        var maxLng = (decimal)(longitude + lngDelta);
        var since = Now - LocationFreshFor;

        var candidates = await _db.UserLocations
            .Where(l => l.PingMeEnabled
                        && l.LastUpdated >= since
                        && l.UserId != userId
                        && l.User.IsActive
                        && l.Latitude >= minLat && l.Latitude <= maxLat
                        && l.Longitude >= minLng && l.Longitude <= maxLng)
            .Select(l => new
            {
                l.UserId,
                l.Latitude,
                l.Longitude,
                l.LastUpdated,
                l.User.FirstName,
                l.User.LastName,
                CompanyName = l.User.ShowCompanyName ? l.User.CompanyName : null,
                ProfileImageUrl = l.User.ShowProfileImage ? l.User.ProfileImageUrl : null
            })
            .ToListAsync();

        return candidates
            .Select(c => new { c, Miles = DistanceMiles(latitude, longitude, (double)c.Latitude, (double)c.Longitude) })
            .Where(x => x.Miles <= radiusMiles)
            .GroupBy(x => x.c.UserId)
            .Select(g => g.OrderBy(x => x.Miles).First())
            .OrderBy(x => x.Miles)
            .Take(MaxResults)
            .Select(x => new NearbyUserDto
            {
                UserId = x.c.UserId,
                FirstName = x.c.FirstName,
                LastName = x.c.LastName,
                CompanyName = x.c.CompanyName,
                ProfileImageUrl = x.c.ProfileImageUrl,
                Latitude = Math.Round((double)x.c.Latitude, 3),
                Longitude = Math.Round((double)x.c.Longitude, 3),
                DistanceMiles = Math.Round(x.Miles, 1),
                LastSeen = x.c.LastUpdated
            })
            .ToList();
    }

    /// <summary>Great-circle distance in miles (haversine).</summary>
    public static double DistanceMiles(double lat1, double lon1, double lat2, double lon2)
    {
        static double Rad(double degrees) => degrees * Math.PI / 180;

        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * EarthRadiusMiles * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static bool IsValid(double latitude, double longitude) =>
        double.IsFinite(latitude) && double.IsFinite(longitude) &&
        latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
}
