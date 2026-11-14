using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.UnitTests;

public class NearbyServiceTests
{
    [Theory]
    // Houston to Austin is about 146 miles; Los Angeles to New York about 2,450 miles.
    [InlineData(29.7604, -95.3698, 30.2672, -97.7431, 146, 3)]
    [InlineData(34.0522, -118.2437, 40.7128, -74.0060, 2445, 15)]
    // Across the date line: Tokyo to Honolulu, about 3,850 miles.
    [InlineData(35.6762, 139.6503, 21.3069, -157.8583, 3850, 30)]
    public void DistanceMiles_MatchesKnownDistances(double lat1, double lon1, double lat2, double lon2, double expected, double tolerance)
    {
        Assert.InRange(NearbyService.DistanceMiles(lat1, lon1, lat2, lon2), expected - tolerance, expected + tolerance);
    }

    [Theory]
    [InlineData(1.01)] // just over the 1-mile limit
    [InlineData(5)]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetNearby_RejectsDistancesOutside25FtTo1Mile(double radiusMiles)
    {
        // Validation happens before any database access, so a context with no server is enough.
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<QRBusinessExchange.Infrastructure.Data.ApplicationDbContext>()
            .UseSqlServer("Server=tcp:127.0.0.1,1;Database=none;Connect Timeout=1;Encrypt=False").Options;
        var service = new NearbyService(new QRBusinessExchange.Infrastructure.Data.ApplicationDbContext(options));

        var ex = await Assert.ThrowsAsync<SsoException>(() => service.GetNearbyAsync(Guid.NewGuid(), 29.76, -95.37, radiusMiles));
        Assert.Equal("invalid_radius", ex.Error);
    }

    [Fact]
    public void DistanceMiles_SamePointIsZero()
    {
        Assert.Equal(0, NearbyService.DistanceMiles(29.76, -95.37, 29.76, -95.37), 6);
    }

    [Fact]
    public void DistanceMiles_IsSymmetric()
    {
        var there = NearbyService.DistanceMiles(29.76, -95.37, 29.80, -95.40);
        var back = NearbyService.DistanceMiles(29.80, -95.40, 29.76, -95.37);
        Assert.Equal(there, back, 9);
    }
}
