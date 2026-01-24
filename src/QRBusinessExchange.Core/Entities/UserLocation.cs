using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.SqlServer.Types;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// Represents a user's geographical location for map display
/// </summary>
public class UserLocation
{
    [Key]
    public Guid LocationId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [Column(TypeName = "decimal(10, 8)")]
    public decimal Latitude { get; set; }

    [Required]
    [Column(TypeName = "decimal(11, 8)")]
    public decimal Longitude { get; set; }

    public bool PingMeEnabled { get; set; } = false;

    public DateTime LastUpdated { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Spatial geography point for efficient distance queries
    /// </summary>
    [Column(TypeName = "geography")]
    public string? GeographyPoint { get; set; }

    // Navigation property
    public virtual User User { get; set; } = null!;
}
