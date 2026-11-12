using Microsoft.Extensions.Options;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Services;
using Xunit;

namespace QRBusinessExchange.UnitTests;

public class JwtSigningKeyTests
{
    private static readonly string StrongKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Missing_key_is_rejected(string? key)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => JwtSettings.GetValidatedKeyBytes(key));
        Assert.Contains("not configured", ex.Message);
    }

    [Theory]
    [InlineData("YourSuperSecretKeyForJWTTokenGenerationMinimum256Bits!")] // was in appsettings.json
    [InlineData("your-production-secret-key-min-32-chars-xxxxxxxxxxxx")]   // deployment guide sample
    [InlineData("your-secret-key-your-secret-key-your-secret-key")]         // setup guide sample
    public void Published_sample_keys_are_rejected(string key)
    {
        var ex = Assert.Throws<InvalidOperationException>(() => JwtSettings.GetValidatedKeyBytes(key));
        Assert.Contains("published sample", ex.Message);
    }

    [Fact]
    public void Short_key_is_rejected()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => JwtSettings.GetValidatedKeyBytes("only-twenty-chars-xx"));
        Assert.Contains("too short", ex.Message);
    }

    [Fact]
    public void Random_key_is_accepted()
    {
        Assert.True(JwtSettings.GetValidatedKeyBytes(StrongKey).Length >= JwtSettings.MinimumKeyBytes);
    }

    [Fact]
    public void JwtService_refuses_to_start_with_the_old_placeholder()
    {
        var settings = Options.Create(new JwtSettings
        {
            SecretKey = "YourSuperSecretKeyForJWTTokenGenerationMinimum256Bits!",
            Issuer = "QRBusinessExchange",
            Audience = "QRBusinessExchangeAPI"
        });

        Assert.Throws<InvalidOperationException>(() => new JwtService(settings));
    }

    [Fact]
    public void Token_signed_with_another_key_is_rejected()
    {
        JwtService Service(string key) => new(Options.Create(new JwtSettings
        {
            SecretKey = key, Issuer = "QRBusinessExchange", Audience = "QRBusinessExchangeAPI", ExpirationMinutes = 5
        }));

        var ours = Service(StrongKey);
        var attacker = Service(Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64)));
        var userId = Guid.NewGuid();

        Assert.Equal(userId, ours.ValidateToken(ours.GenerateAccessToken(userId, "a@example.test")));
        Assert.Null(ours.ValidateToken(attacker.GenerateAccessToken(userId, "a@example.test")));
    }
}
