using Qrbound.Payments.Models;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.UnitTests;

/// <summary>
/// Each business runs its own payments: it turns them on, sets its limits and default expiry, picks
/// who can take payments and whether saved cards are accepted, and sees its own report. Other
/// businesses' settings and payments don't affect it.
/// </summary>
public class BusinessPaymentSettingsTests
{
    private readonly PaymentTestRig _rig = new();
    private readonly User _owner, _cashier, _rival;
    private readonly Business _shop, _otherShop;

    public BusinessPaymentSettingsTests()
    {
        using var db = _rig.NewDb();
        _owner = NewUser("owner@shop.test");
        _cashier = NewUser("cashier@shop.test");
        _rival = NewUser("owner@othershop.test");
        db.Users.AddRange(_owner, _cashier, _rival);

        _shop = new Business { Name = "Corner Coffee", Slug = "corner-coffee", OwnerUserId = _owner.UserId, IsActive = true };
        _otherShop = new Business { Name = "Other Shop", Slug = "other-shop", OwnerUserId = _rival.UserId, IsActive = true };
        db.Businesses.AddRange(_shop, _otherShop);
        db.BusinessMembers.AddRange(
            new BusinessMember { BusinessId = _shop.BusinessId, UserId = _owner.UserId, Role = BusinessRoles.Owner },
            new BusinessMember { BusinessId = _shop.BusinessId, UserId = _cashier.UserId, Role = BusinessRoles.Member },
            new BusinessMember { BusinessId = _otherShop.BusinessId, UserId = _rival.UserId, Role = BusinessRoles.Owner });
        db.SaveChanges();
    }

    [Fact]
    public async Task Payments_are_off_until_an_owner_turns_them_on()
    {
        var settings = await _rig.Settings().GetAsync(_cashier.UserId, _shop.BusinessId);
        Assert.False(settings.PaymentsEnabled);
        Assert.False(settings.CanEdit);
        Assert.False(settings.CanTakePayments);

        var off = await Assert.ThrowsAsync<QrPaymentException>(() => NewRequest(_cashier, 1000));
        Assert.Equal(409, off.StatusCode);

        var notAllowed = await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Settings().UpdateAsync(_cashier.UserId, _shop.BusinessId, new UpdateBusinessPaymentSettingsRequest { PaymentsEnabled = true }));
        Assert.Equal(403, notAllowed.StatusCode);

        await _rig.Settings().UpdateAsync(_owner.UserId, _shop.BusinessId, new UpdateBusinessPaymentSettingsRequest { PaymentsEnabled = true });
        var created = await NewRequest(_cashier, 1000);
        Assert.Equal(QrPaymentStates.Open, created.Status);
    }

    [Fact]
    public async Task One_business_turning_payments_on_does_not_affect_another()
    {
        _rig.EnablePayments(_shop.BusinessId);

        await NewRequest(_owner, 1000);
        var other = await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Payments().CreateAsync(_rival.UserId, _otherShop.BusinessId, new CreateQrPaymentRequest { AmountCents = 1000 }));
        Assert.Equal(409, other.StatusCode);
    }

    [Fact]
    public async Task A_business_cannot_see_or_touch_another_business_payments()
    {
        _rig.EnablePayments(_shop.BusinessId);
        var mine = await NewRequest(_owner, 1000);

        await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().GetAsync(_rival.UserId, _shop.BusinessId, mine.Id));
        await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().CancelAsync(_rival.UserId, _shop.BusinessId, mine.Id));
        // Even through its own business ID, the rival can't reach this request.
        var viaOwn = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().GetAsync(_rival.UserId, _otherShop.BusinessId, mine.Id));
        Assert.Equal(404, viaOwn.StatusCode);
        Assert.Empty(await _rig.Payments().ListAsync(_rival.UserId, _otherShop.BusinessId));
        await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Settings().ReportAsync(_rival.UserId, _shop.BusinessId, null, null));
    }

    [Fact]
    public async Task Business_limits_apply_inside_the_platform_limits()
    {
        _rig.EnablePayments(_shop.BusinessId, s => { s.MinAmountCents = 500; s.MaxAmountCents = 5000; });

        Assert.Equal(400, (await Assert.ThrowsAsync<QrPaymentException>(() => NewRequest(_owner, 499))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<QrPaymentException>(() => NewRequest(_owner, 5001))).StatusCode);
        await NewRequest(_owner, 5000);

        var settings = await _rig.Settings().GetAsync(_owner.UserId, _shop.BusinessId);
        Assert.Equal(500, settings.EffectiveMinAmountCents);
        Assert.Equal(5000, settings.EffectiveMaxAmountCents);

        // A business can't widen the platform's limits or set min above max.
        await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Settings().UpdateAsync(_owner.UserId, _shop.BusinessId,
            new UpdateBusinessPaymentSettingsRequest { PaymentsEnabled = true, MaxAmountCents = 2_000_000 }));
        await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Settings().UpdateAsync(_owner.UserId, _shop.BusinessId,
            new UpdateBusinessPaymentSettingsRequest { PaymentsEnabled = true, MinAmountCents = 3000, MaxAmountCents = 2000 }));
    }

    [Fact]
    public async Task New_QR_codes_use_the_business_default_expiry()
    {
        _rig.EnablePayments(_shop.BusinessId, s => s.DefaultExpiresInMinutes = 120);

        var created = await NewRequest(_owner, 1000);
        Assert.Equal(_rig.Clock.GetUtcNow().UtcDateTime.AddMinutes(120), created.ExpiresAt);

        var custom = await _rig.Payments().CreateAsync(_owner.UserId, _shop.BusinessId,
            new CreateQrPaymentRequest { AmountCents = 1000, ExpiresInMinutes = 15 });
        Assert.Equal(_rig.Clock.GetUtcNow().UtcDateTime.AddMinutes(15), custom.ExpiresAt);
    }

    [Fact]
    public async Task Managers_only_keeps_members_from_taking_or_cancelling_payments()
    {
        _rig.EnablePayments(_shop.BusinessId, s => s.ManagersOnly = true);

        Assert.Equal(403, (await Assert.ThrowsAsync<QrPaymentException>(() => NewRequest(_cashier, 1000))).StatusCode);

        var byOwner = await NewRequest(_owner, 1000);
        Assert.Equal(403, (await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Payments().CancelAsync(_cashier.UserId, _shop.BusinessId, byOwner.Id))).StatusCode);

        // Members can still see the business's payments.
        Assert.Single(await _rig.Payments().ListAsync(_cashier.UserId, _shop.BusinessId));
    }

    [Fact]
    public async Task Turning_payments_off_stops_open_QR_codes_from_being_paid()
    {
        _rig.EnablePayments(_shop.BusinessId);
        var request = await NewRequest(_owner, 1000);

        await _rig.Settings().UpdateAsync(_owner.UserId, _shop.BusinessId, new UpdateBusinessPaymentSettingsRequest { PaymentsEnabled = false });

        var refused = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().PayAsync(request.Id, new QrPaymentPayRequest
        {
            PaymentToken = "9418594164541111", Expiry = "1228", CardholderName = "Pat Payer"
        }));
        Assert.Equal(409, refused.StatusCode);
        Assert.Empty(_rig.Gateway.Charges);

        // It can still be cancelled.
        var cancelled = await _rig.Payments().CancelAsync(_owner.UserId, _shop.BusinessId, request.Id);
        Assert.Equal(QrPaymentStates.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Business_can_refuse_saved_cards()
    {
        _rig.EnablePayments(_shop.BusinessId, s => s.AllowSavedCards = false);
        var request = await NewRequest(_owner, 1000);

        Assert.False((await _rig.Payments().GetPublicAsync(request.Id)).SavedCardsAccepted);
        var refused = await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Payments().BeginSavedCardPaymentAsync(_cashier.UserId, request.Id, Guid.NewGuid()));
        Assert.Equal(409, refused.StatusCode);
    }

    [Fact]
    public async Task Only_owners_and_admins_resolve_reviews_and_see_the_report()
    {
        _rig.EnablePayments(_shop.BusinessId);
        var request = await NewRequest(_owner, 1000);
        _rig.Gateway.Next = () => new CardPointeResponse { StatusMessage = "Error", OutcomeUnknown = true };
        await _rig.Payments().PayAsync(request.Id, new QrPaymentPayRequest { PaymentToken = "9418594164541111", Expiry = "1228", CardholderName = "Pat" });

        Assert.Equal(403, (await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().ResolveAsync(
            _cashier.UserId, _shop.BusinessId, request.Id, new ResolveQrPaymentRequest { Paid = true }))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Settings().ReportAsync(_cashier.UserId, _shop.BusinessId, null, null))).StatusCode);

        var resolved = await _rig.Payments().ResolveAsync(_owner.UserId, _shop.BusinessId, request.Id, new ResolveQrPaymentRequest { Paid = true });
        Assert.Equal(QrPaymentStates.Paid, resolved.Status);
    }

    [Fact]
    public async Task Report_totals_only_this_business_payments()
    {
        _rig.EnablePayments(_shop.BusinessId);
        _rig.EnablePayments(_otherShop.BusinessId);
        var card = new QrPaymentPayRequest { PaymentToken = "9418594164541111", Expiry = "1228", CardholderName = "Pat" };

        var a = await NewRequest(_owner, 1250);
        var b = await NewRequest(_owner, 3499);
        var c = await NewRequest(_owner, 800);
        await NewRequest(_owner, 999); // left open
        await _rig.Payments().PayAsync(a.Id, card);
        await _rig.Payments().PayAsync(b.Id, card);
        await _rig.Payments().CancelAsync(_owner.UserId, _shop.BusinessId, c.Id);

        var theirs = await _rig.Payments().CreateAsync(_rival.UserId, _otherShop.BusinessId, new CreateQrPaymentRequest { AmountCents = 50_000 });
        await _rig.Payments().PayAsync(theirs.Id, card);

        var report = await _rig.Settings().ReportAsync(_owner.UserId, _shop.BusinessId, null, null);
        Assert.Equal(2, report.PaidCount);
        Assert.Equal(1250 + 3499, report.PaidTotalCents);
        Assert.Equal(1, report.CancelledCount);
        Assert.Equal(1, report.OpenCount);
        var day = Assert.Single(report.Days);
        Assert.Equal(4749, day.PaidTotalCents);
    }

    private static User NewUser(string email) =>
        new() { Email = email, FirstName = email.Split('@')[0], LastName = "Test", PasswordHash = "x", IsActive = true };

    private Task<QrPaymentDetails> NewRequest(User user, long cents) =>
        _rig.Payments().CreateAsync(user.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = cents });
}
