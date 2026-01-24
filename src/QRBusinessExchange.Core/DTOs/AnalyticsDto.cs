namespace QRBusinessExchange.Core.DTOs;

/// <summary>
/// DTO representing usage analytics data for a specific date
/// </summary>
public class AnalyticsDto
{
    /// <summary>
    /// Date for which the analytics data applies
    /// </summary>
    public DateTime Date { get; set; }

    /// <summary>
    /// Number of connections made on this date
    /// </summary>
    public int ConnectionsMade { get; set; }

    /// <summary>
    /// Number of QR code scans on this date
    /// </summary>
    public int QRScans { get; set; }

    /// <summary>
    /// Number of profile views on this date
    /// </summary>
    public int ProfileViews { get; set; }

    /// <summary>
    /// Number of map views on this date
    /// </summary>
    public int MapViews { get; set; }
}
