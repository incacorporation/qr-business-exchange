using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Qrbound.Payments.Models;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.UnitTests;

/// <summary>
/// QR payments: staff create a request, anyone with the link pays it once, for the stored amount.
/// Runs on EF Core's in-memory provider (never the shared database) with a fake CardPointe.
/// </summary>
public class QrPaymentServiceTests
{
    private readonly PaymentTestRig _rig = new();
    private FakeCardPointe _gateway => _rig.Gateway;
    private FakeClock _clock => _rig.Clock;
    private readonly User _owner, _stranger;
    private readonly Business _shop;

    public QrPaymentServiceTests()
    {
        using var db = NewDb();
        _owner = new User { Email = "owner@shop.test", FirstName = "Olive", LastName = "Test", PasswordHash = "x", IsActive = true };
        _stranger = new User { Email = "someone@else.test", FirstName = "Sam", LastName = "Test", PasswordHash = "x", IsActive = true };
        db.Users.AddRange(_owner, _stranger);
        _shop = new Business { Name = "Corner Coffee", Slug = "corner-coffee", OwnerUserId = _owner.UserId, IsActive = true };
        db.Businesses.Add(_shop);
        db.BusinessMembers.Add(new BusinessMember { BusinessId = _shop.BusinessId, UserId = _owner.UserId, Role = BusinessRoles.Owner });
        db.SaveChanges();
    }

    [Fact]
    public async Task Payer_pays_the_stored_amount_once()
    {
        var created = await Service().CreateAsync(_owner.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = 1250, Description = "Latte" });

        var info = await Service().GetPublicAsync(created.Id);
        Assert.Equal("Corner Coffee", info.BusinessName);
        Assert.Equal(1250, info.AmountCents);
        Assert.Equal(QrPaymentStates.Open, info.Status);

        var result = await Service().PayAsync(created.Id, Card());

        Assert.True(result.IsSuccess);
        Assert.Equal(1250, _gateway.Charges.Single().AmountCents);
        Assert.Equal(created.InvoiceNumber, _gateway.Charges.Single().OrderId);

        var after = await Service().GetAsync(_owner.UserId, _shop.BusinessId, created.Id);
        Assert.Equal(QrPaymentStates.Paid, after.Status);
        Assert.Equal("RETREF1", after.TransactionId);
        Assert.Equal("Pat Payer", after.PayerName);

        var again = await Assert.ThrowsAsync<QrPaymentException>(() => Service().PayAsync(created.Id, Card()));
        Assert.Equal(409, again.StatusCode);
        Assert.Single(_gateway.Charges);
    }

    [Fact]
    public async Task Only_members_of_the_business_can_create_or_see_requests()
    {
        var denied = await Assert.ThrowsAsync<QrPaymentException>(() =>
            Service().CreateAsync(_stranger.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = 500 }));
        Assert.Equal(404, denied.StatusCode);

        var created = await Service().CreateAsync(_owner.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = 500 });
        await Assert.ThrowsAsync<QrPaymentException>(() => Service().GetAsync(_stranger.UserId, _shop.BusinessId, created.Id));
    }

    [Fact]
    public async Task Amount_outside_the_limits_is_refused()
    {
        var tooSmall = await Assert.ThrowsAsync<QrPaymentException>(() =>
            Service().CreateAsync(_owner.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = 99 }));
        Assert.Equal(400, tooSmall.StatusCode);
    }

    [Fact]
    public async Task Locks_after_five_declines()
    {
        var created = await Service().CreateAsync(_owner.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = 2000 });
        _gateway.Next = () => new CardPointeResponse { StatusMessage = "Declined", ErrorMessage = "Insufficient funds" };

        for (var i = 0; i < QrPaymentStatus.MaxFailedAttempts; i++)
        {
            var declined = await Service().PayAsync(created.Id, Card());
            Assert.False(declined.IsSuccess);
        }

        var info = await Service().GetPublicAsync(created.Id);
        Assert.Equal(QrPaymentStates.Locked, info.Status);

        var locked = await Assert.ThrowsAsync<QrPaymentException>(() => Service().PayAsync(created.Id, Card()));
        Assert.Equal(409, locked.StatusCode);
        Assert.Equal(QrPaymentStatus.MaxFailedAttempts, _gateway.Charges.Count);
    }

    [Fact]
    public async Task Timeout_goes_to_review_and_staff_resolve_it()
    {
        var created = await Service().CreateAsync(_owner.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = 3000 });
        _gateway.Next = () => new CardPointeResponse { StatusMessage = "Error", OutcomeUnknown = true, ErrorMessage = "Timed out" };

        var result = await Service().PayAsync(created.Id, Card());
        Assert.False(result.IsSuccess);
        Assert.Equal(QrPaymentStates.NeedsReview, (await Service().GetPublicAsync(created.Id)).Status);

        // Nobody can pay again while it's being checked.
        await Assert.ThrowsAsync<QrPaymentException>(() => Service().PayAsync(created.Id, Card()));

        var resolved = await Service().ResolveAsync(_owner.UserId, _shop.BusinessId, created.Id,
            new ResolveQrPaymentRequest { Paid = true, TransactionId = "FOUND123" });
        Assert.Equal(QrPaymentStates.Paid, resolved.Status);
        Assert.Equal("FOUND123", resolved.TransactionId);
    }

    [Fact]
    public async Task Expired_link_cannot_be_paid()
    {
        var created = await Service().CreateAsync(_owner.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = 1000, ExpiresInMinutes = 5 });
        _clock.Advance(TimeSpan.FromMinutes(6));

        Assert.Equal(QrPaymentStates.Expired, (await Service().GetPublicAsync(created.Id)).Status);
        var expired = await Assert.ThrowsAsync<QrPaymentException>(() => Service().PayAsync(created.Id, Card()));
        Assert.Equal(409, expired.StatusCode);
        Assert.Empty(_gateway.Charges);
    }

    [Fact]
    public async Task Second_payer_is_refused_while_the_first_charge_is_in_flight()
    {
        var created = await Service().CreateAsync(_owner.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = 1500 });

        QrPaymentException? secondPayer = null;
        _gateway.During = async () =>
            secondPayer = await Assert.ThrowsAsync<QrPaymentException>(() => Service().PayAsync(created.Id, Card()));

        var first = await Service().PayAsync(created.Id, Card());

        Assert.True(first.IsSuccess);
        Assert.Equal(409, secondPayer?.StatusCode);
        Assert.Single(_gateway.Charges);
    }

    [Fact]
    public async Task Cancelled_request_cannot_be_paid()
    {
        var created = await Service().CreateAsync(_owner.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = 800 });
        await Service().CancelAsync(_owner.UserId, _shop.BusinessId, created.Id);

        await Assert.ThrowsAsync<QrPaymentException>(() => Service().PayAsync(created.Id, Card()));
        Assert.Empty(_gateway.Charges);
    }

    // Each call gets its own DbContext over the same in-memory store, like separate HTTP requests.
    private QrPaymentService Service() => _rig.Payments();

    private ApplicationDbContext NewDb() => _rig.NewDb();

    private static QrPaymentPayRequest Card() => new()
    {
        PaymentToken = "9418594164541111",
        Expiry = "1228",
        CardholderName = "Pat Payer",
        Email = "pat@example.test"
    };

}
