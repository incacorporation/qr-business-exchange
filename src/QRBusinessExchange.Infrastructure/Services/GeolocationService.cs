using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Service for geolocation operations including distance calculations and proximity searches
/// </summary>
public class GeolocationService : IGeolocationService
{
    private const decimal EarthRadiusYards = 6967410.41m; // Earth's radius in yards (3959 miles * 1760 yards/mile)
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeolocationService"/> class
    /// </summary>
    /// <param name="context">The application database context</param>
    public GeolocationService(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public decimal CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2)
    {
        try
        {
            // Convert decimal degrees to radians
            var lat1Rad = DegreesToRadians(lat1);
            var lon1Rad = DegreesToRadians(lon1);
            var lat2Rad = DegreesToRadians(lat2);
            var lon2Rad = DegreesToRadians(lon2);

            // Haversine formula
            var dLat = lat2Rad - lat1Rad;
            var dLon = lon2Rad - lon1Rad;

            var a = (decimal)(Math.Pow((double)Math.Sin((double)dLat / 2.0), 2) +
                    Math.Cos((double)lat1Rad) * Math.Cos((double)lat2Rad) *
                    Math.Pow((double)Math.Sin((double)dLon / 2.0), 2));

            var c = (decimal)(2 * Math.Atan2(Math.Sqrt((double)a), Math.Sqrt((double)(1 - a))));

            // Calculate distance in yards
            var distanceYards = EarthRadiusYards * c;

            return Math.Round(distanceYards, 2);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error calculating distance between coordinates", ex);
        }
    }

    /// <inheritdoc/>
    public bool VerifyPaymentLocation(decimal paymentLatitude, decimal paymentLongitude,
        decimal verificationLatitude, decimal verificationLongitude, decimal maxDistanceYards = 1000)
    {
        try
        {
            if (maxDistanceYards <= 0)
            {
                throw new ArgumentException("Maximum distance must be greater than zero", nameof(maxDistanceYards));
            }

            var distance = CalculateDistance(paymentLatitude, paymentLongitude,
                verificationLatitude, verificationLongitude);

            return distance <= maxDistanceYards;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error verifying payment location", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<User>> GetNearbyUsersAsync(decimal latitude, decimal longitude,
        decimal radiusYards, bool pingMeOnly = false)
    {
        try
        {
            if (radiusYards <= 0)
            {
                throw new ArgumentException("Radius must be greater than zero", nameof(radiusYards));
            }

            // Get all user locations with optional Ping Me filter
            var query = _context.UserLocations
                .Include(ul => ul.User)
                .AsQueryable();

            if (pingMeOnly)
            {
                query = query.Where(ul => ul.PingMeEnabled);
            }

            var userLocations = await query.ToListAsync();

            // Filter by distance using Haversine formula
            var nearbyUsers = userLocations
                .Where(ul =>
                {
                    var distance = CalculateDistance(latitude, longitude, ul.Latitude, ul.Longitude);
                    return distance <= radiusYards;
                })
                .Select(ul => ul.User)
                .Where(u => u.IsActive)
                .Distinct()
                .ToList();

            return nearbyUsers;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Error retrieving nearby users", ex);
        }
    }

    /// <summary>
    /// Converts degrees to radians
    /// </summary>
    /// <param name="degrees">The angle in degrees</param>
    /// <returns>The angle in radians</returns>
    private static decimal DegreesToRadians(decimal degrees)
    {
        return degrees * (decimal)Math.PI / 180m;
    }
}
