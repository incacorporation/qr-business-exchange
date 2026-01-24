using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.Core.Interfaces;

/// <summary>
/// Service interface for geolocation operations
/// </summary>
public interface IGeolocationService
{
    /// <summary>
    /// Calculates the distance between two geographic coordinates
    /// </summary>
    /// <param name="lat1">Latitude of the first location</param>
    /// <param name="lon1">Longitude of the first location</param>
    /// <param name="lat2">Latitude of the second location</param>
    /// <param name="lon2">Longitude of the second location</param>
    /// <returns>The distance between the two locations in yards</returns>
    decimal CalculateDistance(decimal lat1, decimal lon1, decimal lat2, decimal lon2);

    /// <summary>
    /// Verifies that payment and verification locations are within acceptable distance
    /// </summary>
    /// <param name="paymentLatitude">Latitude at payment initiation</param>
    /// <param name="paymentLongitude">Longitude at payment initiation</param>
    /// <param name="verificationLatitude">Latitude at payment verification</param>
    /// <param name="verificationLongitude">Longitude at payment verification</param>
    /// <param name="maxDistanceYards">Maximum allowed distance in yards (default: 1000)</param>
    /// <returns>True if locations are within acceptable distance, false otherwise</returns>
    bool VerifyPaymentLocation(decimal paymentLatitude, decimal paymentLongitude, 
        decimal verificationLatitude, decimal verificationLongitude, decimal maxDistanceYards = 1000);

    /// <summary>
    /// Retrieves users within a specified radius of a location
    /// </summary>
    /// <param name="latitude">Center point latitude</param>
    /// <param name="longitude">Center point longitude</param>
    /// <param name="radiusYards">Search radius in yards</param>
    /// <param name="pingMeOnly">If true, only returns users with "Ping Me" enabled</param>
    /// <returns>A collection of nearby users</returns>
    Task<IEnumerable<User>> GetNearbyUsersAsync(decimal latitude, decimal longitude, decimal radiusYards, bool pingMeOnly = false);
}
