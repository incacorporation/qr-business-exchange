using Microsoft.Extensions.Options;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.UnitTests;

public class DeviceLinkServiceTests
{
    private sealed class ManualClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 9, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static readonly Guid Owner = Guid.NewGuid();
    private const string OwnerEmail = "owner@example.test";

    private readonly ManualClock _clock = new();
    private readonly DeviceLinkService _service;

    public DeviceLinkServiceTests()
    {
        _service = new DeviceLinkService(
            Options.Create(new SsoSettings { PortalBaseUrl = "https://portal.test", QrSessionLifetimeSeconds = 180 }),
            _clock);
    }

    private (Guid Id, string LinkSecret) Start()
    {
        var (response, secret) = _service.Create(Owner, OwnerEmail);
        return (response.SessionId, secret);
    }

    [Fact]
    public void HappyPath_ScanConfirmRegisterComplete()
    {
        var (response, linkSecret) = _service.Create(Owner, OwnerEmail);
        Assert.Equal($"https://portal.test/qr/link/{response.SessionId}#k={linkSecret}", response.LinkUrl);
        Assert.Equal(DeviceLinkStatus.Pending, _service.GetForOwner(response.SessionId, Owner).Status);

        var claim = _service.Claim(response.SessionId, linkSecret, "10.0.0.5", "Mozilla/5.0 (Linux; Android 15)");
        Assert.Equal("o***@example.test", claim.AccountHint);

        var status = _service.GetForOwner(response.SessionId, Owner);
        Assert.Equal(DeviceLinkStatus.Scanned, status.Status);
        Assert.Equal("10.0.0.5", status.PhoneIp);
        Assert.Equal(3, status.VerificationChoices.Distinct().Count());
        Assert.Contains(claim.VerificationCode, status.VerificationChoices);

        // The phone may not create a passkey before the computer confirms.
        var early = Assert.Throws<SsoException>(() => _service.RequireConfirmed(response.SessionId, claim.PhoneSecret));
        Assert.Equal("invalid_state", early.Error);

        _service.Confirm(response.SessionId, Owner, claim.VerificationCode);
        Assert.Equal(DeviceLinkStatus.Confirmed, _service.GetForPhone(response.SessionId, claim.PhoneSecret).Status);
        Assert.Equal((Owner, OwnerEmail), _service.RequireConfirmed(response.SessionId, claim.PhoneSecret));

        _service.Complete(response.SessionId, claim.PhoneSecret, "Android phone");
        var done = _service.GetForOwner(response.SessionId, Owner);
        Assert.Equal(DeviceLinkStatus.Completed, done.Status);
        Assert.Equal("Android phone", done.DeviceName);

        // Completed is final: no second passkey from the same link.
        Assert.Throws<SsoException>(() => _service.RequireConfirmed(response.SessionId, claim.PhoneSecret));
    }

    [Fact]
    public void OnlyTheFirstPhoneCanClaim()
    {
        var (id, secret) = Start();
        _service.Claim(id, secret, null, null);

        var ex = Assert.Throws<SsoException>(() => _service.Claim(id, secret, null, null));
        Assert.Equal("already_used", ex.Error);
    }

    [Fact]
    public void WrongLinkSecret_IsNotFound()
    {
        var (id, _) = Start();
        var ex = Assert.Throws<SsoException>(() => _service.Claim(id, "not-the-secret", null, null));
        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public void WrongNumber_CancelsTheLink()
    {
        var (id, secret) = Start();
        var claim = _service.Claim(id, secret, null, null);
        var wrong = _service.GetForOwner(id, Owner).VerificationChoices.First(c => c != claim.VerificationCode);

        var ex = Assert.Throws<SsoException>(() => _service.Confirm(id, Owner, wrong));
        Assert.Equal("verification_mismatch", ex.Error);
        Assert.Equal(DeviceLinkStatus.Denied, _service.GetForPhone(id, claim.PhoneSecret).Status);

        // The right number no longer helps.
        Assert.Throws<SsoException>(() => _service.Confirm(id, Owner, claim.VerificationCode));
        Assert.Throws<SsoException>(() => _service.RequireConfirmed(id, claim.PhoneSecret));
    }

    [Fact]
    public void OtherAccounts_CannotSeeOrConfirm()
    {
        var (id, secret) = Start();
        var claim = _service.Claim(id, secret, null, null);
        var stranger = Guid.NewGuid();

        Assert.Equal(404, Assert.Throws<SsoException>(() => _service.GetForOwner(id, stranger)).StatusCode);
        Assert.Equal(404, Assert.Throws<SsoException>(() => _service.Confirm(id, stranger, claim.VerificationCode)).StatusCode);
    }

    [Fact]
    public void WrongPhoneSecret_IsNotFound()
    {
        var (id, secret) = Start();
        _service.Claim(id, secret, null, null);
        Assert.Equal(404, Assert.Throws<SsoException>(() => _service.GetForPhone(id, "nope")).StatusCode);
    }

    [Fact]
    public void Choices_DoNotChangeBetweenPolls()
    {
        var (id, secret) = Start();
        _service.Claim(id, secret, null, null);

        var first = _service.GetForOwner(id, Owner).VerificationChoices;
        for (var i = 0; i < 5; i++)
        {
            Assert.Equal(first, _service.GetForOwner(id, Owner).VerificationChoices);
        }
    }

    [Fact]
    public void ExpiredLink_CannotBeClaimed()
    {
        var (id, secret) = Start();
        _clock.Now = _clock.Now.AddSeconds(181);

        Assert.Equal(DeviceLinkStatus.Expired, _service.GetForOwner(id, Owner).Status);
        Assert.Equal("already_used", Assert.Throws<SsoException>(() => _service.Claim(id, secret, null, null)).Error);
    }

    [Fact]
    public void Confirming_GivesThePhoneTimeToCreateThePasskey()
    {
        var (id, secret) = Start();
        var claim = _service.Claim(id, secret, null, null);

        _clock.Now = _clock.Now.AddSeconds(170); // 10 s left on the QR code
        _service.Confirm(id, Owner, claim.VerificationCode);

        _clock.Now = _clock.Now.AddSeconds(60);
        Assert.Equal((Owner, OwnerEmail), _service.RequireConfirmed(id, claim.PhoneSecret));
    }

    [Fact]
    public void StartingANewLink_CancelsThePreviousOne()
    {
        var (first, _) = Start();
        Start();
        Assert.Equal(DeviceLinkStatus.Denied, _service.GetForOwner(first, Owner).Status);
    }

    [Fact]
    public void PhoneCancel_StopsTheLink()
    {
        var (id, secret) = Start();
        var claim = _service.Claim(id, secret, null, null);
        _service.CancelFromPhone(id, claim.PhoneSecret);
        Assert.Equal(DeviceLinkStatus.Denied, _service.GetForOwner(id, Owner).Status);
    }

    [Theory]
    [InlineData("jane@example.com", "j***@example.com")]
    [InlineData("x@y.z", "x***@y.z")]
    [InlineData("broken", "***")]
    public void MaskEmail_HidesTheLocalPart(string email, string expected) =>
        Assert.Equal(expected, DeviceLinkService.MaskEmail(email));

    [Fact]
    public void QrLoginChoices_AreStableAndIncludeTheCode()
    {
        for (var i = 0; i < 50; i++)
        {
            var id = Guid.NewGuid();
            var code = (10 + i).ToString();
            var choices = QrLoginService.BuildChoices(id, code);

            Assert.Equal(3, choices.Distinct().Count());
            Assert.Contains(code, choices);
            Assert.All(choices, c => Assert.InRange(int.Parse(c), 10, 99));
            Assert.Equal(choices, QrLoginService.BuildChoices(id, code));
        }
    }
}
