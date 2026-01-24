namespace QRBusinessExchange.Web.Models;

public class UsageDataPoint
{
    public string Label { get; set; } = string.Empty;
    public int Value { get; set; }
}

public class UserLocationDto
{
    public Guid UserId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string? ProfileImageUrl { get; set; }
}
