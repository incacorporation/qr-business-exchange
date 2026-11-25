using Microsoft.EntityFrameworkCore;
using Qrbound.Payments.Models;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.UnitTests;

/// <summary>
/// Fingerprint checkout: a signed-in customer pays a QR payment request with a card on file after
/// confirming with their passkey. The passkey check is the real WebAuthnService, fed assertions
/// signed by a test passkey; CardPointe is faked.
/// </summary>
public class SavedCardPaymentTests : IDisposable
{
    private readonly PaymentTestRig _rig = new();
    private readonly TestPasskey _patPhone = new();
    private readonly TestPasskey _samPhone = new();
    private readonly User _owner, _pat, _sam;
    private readonly Business _shop;

    public SavedCardPaymentTests()
    {
        using var db = _rig.NewDb();
        _owner = NewUser("owner@shop.test", "Olive");
        _pat = NewUser("pat@example.test", "Pat");
        _sam = NewUser("sam@example.test", "Sam");
        db.Users.AddRange(_owner, _pat, _sam);
        _shop = new Business { Name = "Corner Coffee", Slug = "corner-coffee", OwnerUserId = _owner.UserId, IsActive = true };
        db.Businesses.Add(_shop);
        db.BusinessMembers.Add(new BusinessMember { BusinessId = _shop.BusinessId, UserId = _owner.UserId, Role = BusinessRoles.Owner });
        db.PasskeyCredentials.Add(_patPhone.CredentialFor(_pat.UserId));
        db.PasskeyCredentials.Add(_samPhone.CredentialFor(_sam.UserId));
        db.SaveChanges();
    }

    [Fact]
    public async Task Pays_with_a_saved_card_after_the_fingerprint()
    {
        var card = await SaveCard(_pat);
        var request = await NewRequest(3499);

        var options = await _rig.Payments().BeginSavedCardPaymentAsync(_pat.UserId, request.Id, card.Id);
        Assert.Equal("required", options.UserVerification);
        Assert.NotNull(options.AllowCredentials);

        var result = await _rig.Payments().PaySavedCardAsync(_pat.UserId, request.Id, new SavedCardPayRequest
        {
            SavedCardId = card.Id, Challenge = options.Challenge, Assertion = _patPhone.Sign(options)
        });

        Assert.True(result.IsSuccess);
        var charge = Assert.Single(_rig.Gateway.Charges);
        Assert.Equal("PROFILE1", charge.ProfileId);
        Assert.Null(charge.Token);
        Assert.Equal(3499, charge.AmountCents);

        var paid = await _rig.Payments().GetAsync(_owner.UserId, _shop.BusinessId, request.Id);
        Assert.Equal(QrPaymentStates.Paid, paid.Status);
        Assert.Equal("Pat Test", paid.PayerName);
        Assert.Equal("pat@example.test", paid.PayerEmail);

        using var db = _rig.NewDb();
        Assert.NotNull((await db.SavedCards.SingleAsync(c => c.SavedCardId == card.Id)).LastUsedAt);
    }

    [Fact]
    public async Task Fingerprint_for_one_card_cannot_pay_with_another()
    {
        var cardA = await SaveCard(_pat, "9418594164541111");
        var cardB = await SaveCard(_pat, "9518594164542222");
        var request = await NewRequest(2000);

        // Approved for card A, submitted for card B: the challenge was bound to A.
        var options = await _rig.Payments().BeginSavedCardPaymentAsync(_pat.UserId, request.Id, cardA.Id);
        var assertion = _patPhone.Sign(options);
        var swapped = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().PaySavedCardAsync(_pat.UserId, request.Id,
            new SavedCardPayRequest { SavedCardId = cardB.Id, Challenge = options.Challenge, Assertion = assertion }));
        Assert.Equal(401, swapped.StatusCode);

        // The challenge is single use: replaying the same assertion for card A now fails too.
        var replay = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().PaySavedCardAsync(_pat.UserId, request.Id,
            new SavedCardPayRequest { SavedCardId = cardA.Id, Challenge = options.Challenge, Assertion = assertion }));
        Assert.Equal(401, replay.StatusCode);

        Assert.Empty(_rig.Gateway.Charges);
    }

    [Fact]
    public async Task Fingerprint_for_one_payment_cannot_pay_another()
    {
        var card = await SaveCard(_pat);
        var coffee = await NewRequest(500);
        var laptop = await NewRequest(99_900);

        var options = await _rig.Payments().BeginSavedCardPaymentAsync(_pat.UserId, coffee.Id, card.Id);
        var denied = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().PaySavedCardAsync(_pat.UserId, laptop.Id,
            new SavedCardPayRequest { SavedCardId = card.Id, Challenge = options.Challenge, Assertion = _patPhone.Sign(options) }));

        Assert.Equal(401, denied.StatusCode);
        Assert.Empty(_rig.Gateway.Charges);
    }

    [Fact]
    public async Task Someone_elses_passkey_cannot_approve_my_card()
    {
        var card = await SaveCard(_pat);
        var request = await NewRequest(1200);

        var options = await _rig.Payments().BeginSavedCardPaymentAsync(_pat.UserId, request.Id, card.Id);
        var denied = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().PaySavedCardAsync(_pat.UserId, request.Id,
            new SavedCardPayRequest { SavedCardId = card.Id, Challenge = options.Challenge, Assertion = _samPhone.Sign(options) }));

        Assert.Equal(401, denied.StatusCode);
        Assert.Empty(_rig.Gateway.Charges);
    }

    [Fact]
    public async Task Cannot_use_another_users_saved_card()
    {
        var patsCard = await SaveCard(_pat);
        var request = await NewRequest(1200);

        var denied = await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Payments().BeginSavedCardPaymentAsync(_sam.UserId, request.Id, patsCard.Id));
        Assert.Equal(404, denied.StatusCode);
    }

    [Fact]
    public async Task Needs_a_passkey_on_the_account()
    {
        var card = await SaveCard(_owner); // Olive has no passkey
        var request = await NewRequest(1200);

        var denied = await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Payments().BeginSavedCardPaymentAsync(_owner.UserId, request.Id, card.Id));
        Assert.Equal(409, denied.StatusCode);
    }

    [Fact]
    public async Task Expired_cards_cannot_be_saved_or_used()
    {
        var expired = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.SavedCards().AddAsync(_pat.UserId,
            new SaveCardRequest { PaymentToken = "9418594164541111", Expiry = "1026", CardholderName = "Pat Test" }));
        Assert.Equal(400, expired.StatusCode);

        var card = await SaveCard(_pat, expiry: "1226");
        var request = await NewRequest(1200);
        _rig.Clock.Advance(TimeSpan.FromDays(40)); // into January 2027

        var denied = await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Payments().BeginSavedCardPaymentAsync(_pat.UserId, request.Id, card.Id));
        Assert.Equal(409, denied.StatusCode);
        Assert.True((await _rig.SavedCards().ListAsync(_pat.UserId)).Single().IsExpired);
    }

    [Fact]
    public async Task Removing_a_card_deletes_it_from_CardPointe()
    {
        var card = await SaveCard(_pat);
        Assert.Equal("1111", card.Last4);

        await _rig.SavedCards().RemoveAsync(_pat.UserId, card.Id);

        Assert.Equal(new[] { "PROFILE1/1" }, _rig.Gateway.DeletedProfiles);
        Assert.Empty(await _rig.SavedCards().ListAsync(_pat.UserId));

        var notMine = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.SavedCards().RemoveAsync(_sam.UserId, card.Id));
        Assert.Equal(404, notMine.StatusCode);
    }

    [Fact]
    public async Task At_most_five_cards()
    {
        for (var i = 0; i < SavedCard.MaxPerUser; i++)
        {
            await SaveCard(_pat, $"941859416454{1000 + i}");
        }

        var tooMany = await Assert.ThrowsAsync<QrPaymentException>(() => SaveCard(_pat, "9418594164549999"));
        Assert.Equal(409, tooMany.StatusCode);
    }

    private static User NewUser(string email, string first) =>
        new() { Email = email, FirstName = first, LastName = "Test", PasswordHash = "x", IsActive = true, IsEmailVerified = true };

    private Task<SavedCardDto> SaveCard(User user, string token = "9418594164541111", string expiry = "1228") =>
        _rig.SavedCards().AddAsync(user.UserId, new SaveCardRequest
        {
            PaymentToken = token, Expiry = expiry, CardholderName = $"{user.FirstName} Test"
        });

    private Task<QrPaymentDetails> NewRequest(long cents) =>
        _rig.Payments().CreateAsync(_owner.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = cents });

    public void Dispose()
    {
        _patPhone.Dispose();
        _samPhone.Dispose();
    }
}
