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

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public QRBusinessExchange.Core.DTOs.UserProfileDto User { get; set; } = new();
    public QRBusinessExchange.Core.DTOs.SubscriptionDto Subscription { get; set; } = new();
}

public class AuthResponse
{
	public string AccessToken { get; set; } = string.Empty;
	public string RefreshToken { get; set; } = string.Empty;
	public DateTime ExpiresAt { get; set; }
	public QRBusinessExchange.Core.DTOs.UserProfileDto User { get; set; } = new();
	public QRBusinessExchange.Core.DTOs.SubscriptionDto Subscription { get; set; } = new();
}

public class Advertisement
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? Link { get; set; }
}
