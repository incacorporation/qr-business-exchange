using System.Text;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.UnitTests;

public class ProfileImageServiceTests
{
    [Fact]
    public void Jpeg_IsDetectedFromItsBytes() =>
        Assert.Equal("image/jpeg", ProfileImageService.DetectContentType(new byte[] { 0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10 }));

    [Fact]
    public void Png_IsDetectedFromItsBytes() =>
        Assert.Equal("image/png", ProfileImageService.DetectContentType(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0 }));

    [Theory]
    [InlineData("GIF87a")]
    [InlineData("GIF89a")]
    public void Gif_IsDetectedFromItsBytes(string header) =>
        Assert.Equal("image/gif", ProfileImageService.DetectContentType(Encoding.ASCII.GetBytes(header + "\u0001\u0000")));

    [Theory]
    [InlineData("<html><script>alert(1)</script></html>")] // HTML renamed to .png
    [InlineData("<svg xmlns=\"http://www.w3.org/2000/svg\" onload=\"alert(1)\"/>")] // SVG can carry script
    [InlineData("%PDF-1.7")]
    [InlineData("RIFF....WEBPVP8 ")] // WebP isn't one of the allowed types
    [InlineData("")]
    public void OtherContent_IsRejected(string content) =>
        Assert.Null(ProfileImageService.DetectContentType(Encoding.ASCII.GetBytes(content)));

    [Fact]
    public void TruncatedHeaders_AreRejected()
    {
        Assert.Null(ProfileImageService.DetectContentType(new byte[] { 0xFF, 0xD8 }));
        Assert.Null(ProfileImageService.DetectContentType(new byte[] { 0x89, 0x50, 0x4E, 0x47 }));
        Assert.Null(ProfileImageService.DetectContentType(Encoding.ASCII.GetBytes("GIF8")));
    }
}
