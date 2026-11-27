using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Qrbound.Payments.Models;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Data;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.IntegrationTests;

/// <summary>
/// The QR payment flow over HTTP: staff create a request, an anonymous payer reads and pays it,
/// staff see it paid. Uses an in-memory database (never the configured SQL Server) and the mock
/// CardPointe gateway, so no card is charged.
/// </summary>
public class QrPaymentFlowTests : IDisposable
{
    private static readonly string TestJwtKey = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(64));

    private readonly WebApplicationFactory<Program> _factory;
    private readonly string _dbName = "qr-pay-" + Guid.NewGuid();
    private readonly Guid _ownerId = Guid.NewGuid();
    private readonly Guid _businessId = Guid.NewGuid();

    public QrPaymentFlowTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("JwtSettings:SecretKey", TestJwtKey);
            // Never the shared database: the context below is replaced, and this address goes nowhere.
            builder.UseSetting("ConnectionStrings:DefaultConnection",
                "Server=(localdb)\\NoSuchInstance;Database=none;Integrated Security=True;Encrypt=Mandatory;TrustServerCertificate=True");
            builder.UseSetting("Admin:SeedPassword", "");
            builder.UseSetting("CardPointe:UseMockApi", "true");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(_dbName));
            });
        });

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.Users.Add(new User { UserId = _ownerId, Email = "owner@shop.test", FirstName = "Olive", LastName = "Test", PasswordHash = "x", IsActive = true, IsEmailVerified = true });
        db.Businesses.Add(new Business { BusinessId = _businessId, Name = "Corner Coffee", Slug = "corner-coffee-" + Guid.NewGuid().ToString("N")[..6], OwnerUserId = _ownerId, IsActive = true });
        db.BusinessMembers.Add(new BusinessMember { BusinessId = _businessId, UserId = _ownerId, Role = BusinessRoles.Owner });
        db.BusinessPaymentSettings.Add(new BusinessPaymentSettings { BusinessId = _businessId, PaymentsEnabled = true });
        db.SaveChanges();
    }

    [Fact]
    public async Task Staff_create_payer_pays_staff_see_it_paid()
    {
        var staff = StaffClient();
        var anonymous = _factory.CreateClient();

        // Staff create a $12.50 request.
        var create = await staff.PostAsJsonAsync($"/api/businesses/{_businessId}/qr-payments",
            new CreateQrPaymentRequest { AmountCents = 1250, Description = "2 lattes" });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = (await create.Content.ReadFromJsonAsync<QrPaymentDetails>())!;
        Assert.Equal(QrPaymentStates.Open, created.Status);

        // Anyone with the link sees the business and amount.
        var info = (await anonymous.GetFromJsonAsync<QrPaymentInfo>($"/api/pay/{created.Id}"))!;
        Assert.Equal("Corner Coffee", info.BusinessName);
        Assert.Equal(1250, info.AmountCents);
        Assert.Equal("2 lattes", info.Description);

        // The public config the card form needs is available without signing in.
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/api/cardpointe/config")).StatusCode);

        // They pay without an account (mock gateway approves).
        var pay = await anonymous.PostAsJsonAsync($"/api/pay/{created.Id}", Card());
        Assert.Equal(HttpStatusCode.OK, pay.StatusCode);
        var result = (await pay.Content.ReadFromJsonAsync<CardPaymentResult>())!;
        Assert.True(result.IsSuccess);
        Assert.Equal(1250, result.AmountCents);

        // Paying again is refused.
        var again = await anonymous.PostAsJsonAsync($"/api/pay/{created.Id}", Card());
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // Staff see it paid, with the payer's name.
        var seen = (await staff.GetFromJsonAsync<QrPaymentDetails>($"/api/businesses/{_businessId}/qr-payments/{created.Id}"))!;
        Assert.Equal(QrPaymentStates.Paid, seen.Status);
        Assert.Equal("Pat Payer", seen.PayerName);
        Assert.NotNull(seen.TransactionId);

        var list = (await staff.GetFromJsonAsync<List<QrPaymentDetails>>($"/api/businesses/{_businessId}/qr-payments"))!;
        Assert.Contains(list, p => p.Id == created.Id && p.Status == QrPaymentStates.Paid);
    }

    [Fact]
    public async Task Staff_endpoints_need_sign_in_and_pay_needs_a_valid_card()
    {
        var anonymous = _factory.CreateClient();

        var create = await anonymous.PostAsJsonAsync($"/api/businesses/{_businessId}/qr-payments",
            new CreateQrPaymentRequest { AmountCents = 1250 });
        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);

        var created = (await (await StaffClient().PostAsJsonAsync($"/api/businesses/{_businessId}/qr-payments",
            new CreateQrPaymentRequest { AmountCents = 500 })).Content.ReadFromJsonAsync<QrPaymentDetails>())!;

        var badExpiry = await anonymous.PostAsJsonAsync($"/api/pay/{created.Id}", new QrPaymentPayRequest
        {
            PaymentToken = "9418594164541111", Expiry = "202812", CardholderName = "Pat Payer"
        });
        Assert.Equal(HttpStatusCode.BadRequest, badExpiry.StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/pay/{Guid.NewGuid()}")).StatusCode);

        // Paying with a saved card needs a signed-in customer; the public pay endpoints don't.
        var savedCardInit = await anonymous.PostAsJsonAsync($"/api/pay/{created.Id}/saved-card/init", new { savedCardId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, savedCardInit.StatusCode);
        var savedCardPay = await anonymous.PostAsJsonAsync($"/api/pay/{created.Id}/saved-card", new { savedCardId = Guid.NewGuid(), challenge = "x", assertion = new { } });
        Assert.Equal(HttpStatusCode.Unauthorized, savedCardPay.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/pay/{created.Id}")).StatusCode);
    }

    private HttpClient StaffClient()
    {
        using var scope = _factory.Services.CreateScope();
        var token = scope.ServiceProvider.GetRequiredService<JwtService>().GenerateAccessToken(_ownerId, "owner@shop.test");
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static QrPaymentPayRequest Card() => new()
    {
        PaymentToken = "9418594164541111",
        Expiry = "1228",
        CardholderName = "Pat Payer",
        Email = "pat@example.test"
    };

    public void Dispose() => _factory.Dispose();
}
