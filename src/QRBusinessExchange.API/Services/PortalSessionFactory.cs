using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Options;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Services;

/// <summary>
/// Issues the portal (core) session token. The "amr" claim records how the user
/// signed in, which the SSO authorize endpoint uses to enforce passkey-only SSO.
/// </summary>
public class PortalSessionFactory
{
    private readonly JwtService _jwtService;
    private readonly JwtSettings _jwtSettings;

    public PortalSessionFactory(JwtService jwtService, IOptions<JwtSettings> jwtSettings)
    {
        _jwtService = jwtService;
        _jwtSettings = jwtSettings.Value;
    }

    public AuthResponse Create(User user, string authMethod)
    {
        var accessToken = _jwtService.GenerateAccessToken(user.UserId, user.Email, new Dictionary<string, string>
        {
            { "role", user.Role },
            { JwtRegisteredClaimNames.Amr, authMethod },
            { JwtRegisteredClaimNames.AuthTime, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
        });

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = _jwtService.GenerateRefreshToken(),
            ExpiresAt = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpirationMinutes),
            User = ToDto(user)
        };
    }

    public static UserProfileDto ToDto(User user) => new()
    {
        UserId = user.UserId,
        Email = user.Email,
        FirstName = user.FirstName,
        LastName = user.LastName,
        CompanyName = user.CompanyName,
        PhoneNumber = user.PhoneNumber,
        ProfileImageUrl = user.ProfileImageUrl,
        Bio = user.Bio,
        Skills = user.Skills,
        ServicePricing = user.ServicePricing,
        IsEmailVerified = user.IsEmailVerified,
        Role = user.Role,
        CreatedAt = user.CreatedAt
    };
}
