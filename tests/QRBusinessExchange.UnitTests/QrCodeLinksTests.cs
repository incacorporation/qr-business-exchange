using QRBusinessExchange.Client.Services;

namespace QRBusinessExchange.UnitTests;

public class QrCodeLinksTests
{
    private static readonly Uri Site = new("https://www.qrbound.com/");
    private const string Id = "0f8fad5b-d9cb-469f-a165-70867728950e";

    [Theory]
    [InlineData($"https://www.qrbound.com/user/{Id}")]
    [InlineData($"https://qrbound.com/user/{Id}")]
    [InlineData($"  https://www.qrbound.com/user/{Id}  ")]
    public void BusinessCard_OnThisSite_ReturnsUserId(string scanned)
    {
        Assert.Equal(Guid.Parse(Id), QrCodeLinks.ToBusinessCardUserId(scanned, Site));
    }

    [Theory]
    [InlineData($"https://evil.example/user/{Id}")]
    [InlineData($"http://www.qrbound.com/user/{Id}")]
    [InlineData($"https://www.qrbound.com/user/{Id}?x=1")]
    [InlineData($"https://www.qrbound.com/user/{Id}#k=1")]
    [InlineData($"https://www.qrbound.com/qr/approve/{Id}")]
    [InlineData("https://www.qrbound.com/user/not-a-guid")]
    [InlineData($"https://someone@www.qrbound.com/user/{Id}")]
    [InlineData("hello")]
    [InlineData("")]
    [InlineData(null)]
    public void NotABusinessCard_ReturnsNull(string? scanned)
    {
        Assert.Null(QrCodeLinks.ToBusinessCardUserId(scanned, Site));
    }

    [Theory]
    [InlineData($"https://www.qrbound.com/user/{Id}", $"/user/{Id}")]
    [InlineData($"https://www.qrbound.com/qr/approve/{Id}", $"/qr/approve/{Id}")]
    [InlineData($"https://qrbound.com/qr/link/{Id}#k=abc", $"/qr/link/{Id}#k=abc")]
    [InlineData($"https://www.qrbound.com/pay/{Id}", $"/pay/{Id}")]
    public void AppRoute_AllowsOwnQrFlowsAndBusinessCards(string scanned, string expected)
    {
        Assert.Equal(expected, QrCodeLinks.ToAppRoute(scanned, Site));
    }

    [Theory]
    [InlineData("https://www.qrbound.com/settings")]
    [InlineData($"https://www.qrbound.com/user/{Id}/extra")]
    [InlineData($"https://other.example/qr/approve/{Id}")]
    public void AppRoute_RejectsOtherPagesAndSites(string scanned)
    {
        Assert.Null(QrCodeLinks.ToAppRoute(scanned, Site));
    }
}
