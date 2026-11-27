using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.UnitTests;

/// <summary>Rules used when external apps (Qrbound.SignIn) collect QR sign-ins.</summary>
public class QrExternalAppTests
{
    private static readonly SsoSettings Settings = new()
    {
        PortalBaseUrl = "https://www.qrbound.com",
        PortalOrigins = { "https://localhost:7090" }
    };

    [Theory]
    [InlineData("https://www.qrbound.com")]
    [InlineData("https://WWW.qrbound.com")]
    [InlineData("https://localhost:7090")]
    public void Portal_origins_are_recognised(string origin) =>
        Assert.True(Settings.IsPortalOrigin(origin));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("https://evil.example")]
    [InlineData("http://www.qrbound.com")]           // scheme differs
    [InlineData("https://www.qrbound.com:8443")]     // port differs
    [InlineData("https://www.qrbound.com.evil.example")]
    [InlineData("https://localhost:7091")]
    public void Other_origins_are_not_the_portal(string? origin) =>
        Assert.False(Settings.IsPortalOrigin(origin));

    [Fact]
    public void Public_apps_must_send_a_code_challenge()
    {
        var ex = Assert.Throws<SsoException>(() => SsoService.ValidatePkce(null, null, isConfidential: false));
        Assert.Equal("invalid_request", ex.Error);
        SsoService.ValidatePkce(null, null, isConfidential: true); // confidential apps may skip PKCE
    }

    [Fact]
    public void Only_S256_with_a_valid_length_is_accepted()
    {
        var challenge = SsoCrypto.PkceS256(SsoCrypto.NewSecret());
        SsoService.ValidatePkce(challenge, "S256", false);
        SsoService.ValidatePkce(challenge, null, false);  // defaults to S256
        Assert.Throws<SsoException>(() => SsoService.ValidatePkce(challenge, "plain", false));
        Assert.Throws<SsoException>(() => SsoService.ValidatePkce("short", "S256", false));
        Assert.Throws<SsoException>(() => SsoService.ValidatePkce(new string('a', 129), "S256", false));
    }
}
