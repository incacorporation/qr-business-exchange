using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QRBusinessExchange.Core.Entities;

/// <summary>
/// Represents daily usage analytics for users
/// </summary>
public class UsageAnalytics
{
    [Key]
    public Guid AnalyticsId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [Column(TypeName = "date")]
    public DateTime Date { get; set; }

    public int ConnectionsMade { get; set; } = 0;

    public int QRScans { get; set; } = 0;

    public int ProfileViews { get; set; } = 0;

    public int MapViews { get; set; } = 0;

    // Navigation property
    public virtual User User { get; set; } = null!;
}
