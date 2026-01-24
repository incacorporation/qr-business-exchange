using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.Core.DTOs;

/// <summary>
/// DTO representing a user's geographic location
/// </summary>
public class LocationDto
{
    /// <summary>
    /// Latitude coordinate
    /// </summary>
    [Required(ErrorMessage = "Latitude is required")]
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
    public decimal Latitude { get; set; }

    /// <summary>
    /// Longitude coordinate
    /// </summary>
    [Required(ErrorMessage = "Longitude is required")]
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
    public decimal Longitude { get; set; }

    /// <summary>
    /// Indicates if the user has enabled the "Ping Me" feature
    /// </summary>
    public bool PingMeEnabled { get; set; }
}
