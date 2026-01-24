using System.ComponentModel.DataAnnotations;

namespace QRBusinessExchange.API.DTOs;

public class RegisterRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;

    public string? CompanyName { get; set; }
    public string? PhoneNumber { get; set; }
}

public class LoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserDto User { get; set; } = null!;
}

public class RefreshTokenRequest
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class UserDto
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ProfileImageUrl { get; set; }
    public string? Bio { get; set; }
    public string? Skills { get; set; }
    public decimal? ServicePricing { get; set; }
    public bool IsEmailVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class UpdateProfileRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? CompanyName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Bio { get; set; }
    public string? Skills { get; set; }
    public decimal? ServicePricing { get; set; }
}

public class UpdateLocationRequest
{
    [Required]
    public decimal Latitude { get; set; }

    [Required]
    public decimal Longitude { get; set; }

    public bool PingMeEnabled { get; set; }
}

public class ConnectionRequest
{
    [Required]
    public Guid RecipientUserId { get; set; }

    public string? Message { get; set; }
}

public class ConnectionDto
{
    public Guid ConnectionId { get; set; }
    public Guid InitiatorUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime ConnectionDate { get; set; }
    public DateTime? ResponseDate { get; set; }
    public string? Message { get; set; }
    public UserDto? InitiatorUser { get; set; }
    public UserDto? RecipientUser { get; set; }
}

public class SubscriptionDto
{
    public Guid SubscriptionId { get; set; }
    public string SubscriptionType { get; set; } = string.Empty;
    public int CreditsAvailable { get; set; }
    public int CreditsUsed { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public bool AutoRenew { get; set; }
    public bool IsActive { get; set; }
}

public class AdvertisementRequest
{
    public string? AdContent { get; set; }
    public string? ImageUrl { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }
}

public class AdvertisementDto
{
    public Guid AdId { get; set; }
    public string? AdContent { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal CostPerDay { get; set; }
    public decimal TotalCost { get; set; }
    public bool IsActive { get; set; }
    public int Impressions { get; set; }
    public int Clicks { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class PaymentRequest
{
    [Required]
    public decimal Amount { get; set; }

    [Required]
    public string Currency { get; set; } = "USD";

    [Required]
    public string PaymentGateway { get; set; } = string.Empty;

    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
}

public class PaymentDto
{
    public Guid PaymentId { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string PaymentGateway { get; set; } = string.Empty;
    public string? TransactionId { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal? PaymentLatitude { get; set; }
    public decimal? PaymentLongitude { get; set; }
    public decimal? VerificationLatitude { get; set; }
    public decimal? VerificationLongitude { get; set; }
    public decimal? DistanceVerification { get; set; }
    public bool IsVerified { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NearbyUsersRequest
{
    [Required]
    public decimal Latitude { get; set; }

    [Required]
    public decimal Longitude { get; set; }

    public int RadiusYards { get; set; } = 1000;
}

public class UserLocationDto
{
    public Guid UserId { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public bool PingMeEnabled { get; set; }
    public decimal? DistanceYards { get; set; }
    public UserDto? User { get; set; }
}
