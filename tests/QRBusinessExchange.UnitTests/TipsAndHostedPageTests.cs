using Qrbound.Payments.Models;
using QRBusinessExchange.API.Services;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.UnitTests;

/// <summary>
/// Tips and Fiserv's Hosted Payment Page: the customer locks a tip into a quote; every way of paying
/// charges exactly that total; payments made on the hosted page are only recorded once CardPointe
/// (faked here) reports them.
/// </summary>
public class TipsAndHostedPageTests : IDisposable
{
    private readonly PaymentTestRig _rig = new();
    private readonly TestPasskey _patPhone = new();
    private readonly User _owner, _pat;
    private readonly Business _shop;

    public TipsAndHostedPageTests()
    {
        using var db = _rig.NewDb();
        _owner = NewUser("owner@shop.test", "Olive");
        _pat = NewUser("pat@example.test", "Pat");
        db.Users.AddRange(_owner, _pat);
        _shop = new Business { Name = "Corner Coffee", Slug = "corner-coffee", OwnerUserId = _owner.UserId, IsActive = true };
        db.Businesses.Add(_shop);
        db.BusinessMembers.Add(new BusinessMember { BusinessId = _shop.BusinessId, UserId = _owner.UserId, Role = BusinessRoles.Owner });
        db.PasskeyCredentials.Add(_patPhone.CredentialFor(_pat.UserId));
        db.SaveChanges();
        _rig.EnablePayments(_shop.BusinessId, s =>
        {
            s.TipsEnabled = true;
            s.TipPercentages = "15,18,20";
            s.AllowCustomTip = true;
            s.MaxTipPercent = 30;
        });
    }

    // Tips and quotes ------------------------------------------------------------

    [Fact]
    public async Task Customer_sees_the_tip_options()
    {
        var bill = await NewBill(4000);
        var info = await _rig.Payments().GetPublicAsync(bill.Id);

        Assert.True(info.TipsEnabled);
        Assert.Equal(new[] { 15, 18, 20 }, info.TipPercentages);
        Assert.True(info.AllowCustomTip);
        Assert.Equal(30, info.MaxTipPercent);
        Assert.True(info.HostedPageAvailable);
        Assert.False(info.HostedPageStarted);
    }

    [Fact]
    public async Task Card_payment_charges_the_quoted_total_and_records_the_tip()
    {
        var bill = await NewBill(4000);
        var quote = await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 720 }); // 18%
        Assert.Equal(4720, quote.TotalCents);

        var result = await _rig.Payments().PayAsync(bill.Id, Card(quote.QuoteId));

        Assert.True(result.IsSuccess);
        Assert.Equal(4720, result.AmountCents);
        Assert.Equal(4720, Assert.Single(_rig.Gateway.Charges).AmountCents);

        var paid = await Details(bill.Id);
        Assert.Equal(QrPaymentStates.Paid, paid.Status);
        Assert.Equal(720, paid.TipCents);
        Assert.Equal(4720, paid.PaidAmountCents);
        Assert.Equal(QrPaymentMethods.Card, paid.PaymentMethod);

        var info = await _rig.Payments().GetPublicAsync(bill.Id);
        Assert.Equal(720, info.TipCents);
        Assert.Equal(4720, info.PaidAmountCents);
    }

    [Fact]
    public async Task Paying_without_a_quote_charges_the_bill_alone()
    {
        var bill = await NewBill(4000);
        await _rig.Payments().PayAsync(bill.Id, Card(null));

        Assert.Equal(4000, Assert.Single(_rig.Gateway.Charges).AmountCents);
        Assert.Equal(0, (await Details(bill.Id)).TipCents);
    }

    [Fact]
    public async Task Tips_outside_the_business_rules_are_refused()
    {
        var bill = await NewBill(4000);

        var tooBig = await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 1201 })); // over 30%
        Assert.Equal(400, tooBig.StatusCode);

        var max = await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 1200 });
        Assert.Equal(5200, max.TotalCents);

        // Without custom tips only the suggested amounts are accepted.
        await UpdateTips(tipsEnabled: true, allowCustom: false);
        var custom = await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 500 }));
        Assert.Equal(400, custom.StatusCode);
        Assert.Equal(4600, (await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 600 })).TotalCents);

        await UpdateTips(tipsEnabled: false, allowCustom: true);
        var off = await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 600 }));
        Assert.Equal(400, off.StatusCode);
        Assert.Equal(4000, (await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 0 })).TotalCents);
    }

    [Fact]
    public async Task A_replaced_or_timed_out_quote_cannot_be_paid()
    {
        var bill = await NewBill(4000);
        var first = await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 600 });
        var second = await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 800 });

        var replaced = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().PayAsync(bill.Id, Card(first.QuoteId)));
        Assert.Equal(409, replaced.StatusCode);

        _rig.Clock.Advance(QrPaymentService.QuoteLifetime + TimeSpan.FromSeconds(1));
        var timedOut = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().PayAsync(bill.Id, Card(second.QuoteId)));
        Assert.Equal(409, timedOut.StatusCode);
        Assert.Empty(_rig.Gateway.Charges);
    }

    // Fingerprint over the quote -----------------------------------------------------

    [Fact]
    public async Task Fingerprint_approves_the_quoted_total()
    {
        var card = await SaveCard();
        var bill = await NewBill(4000);
        var quote = await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 800 });

        var options = await _rig.Payments().BeginSavedCardPaymentAsync(_pat.UserId, bill.Id, card.Id, quote.QuoteId);
        var result = await _rig.Payments().PaySavedCardAsync(_pat.UserId, bill.Id, new SavedCardPayRequest
        {
            SavedCardId = card.Id, QuoteId = quote.QuoteId, Challenge = options.Challenge, Assertion = _patPhone.Sign(options)
        });

        Assert.True(result.IsSuccess);
        Assert.Equal(4800, Assert.Single(_rig.Gateway.Charges).AmountCents);
        var paid = await Details(bill.Id);
        Assert.Equal(800, paid.TipCents);
        Assert.Equal(QrPaymentMethods.SavedCard, paid.PaymentMethod);
    }

    [Fact]
    public async Task Fingerprint_for_one_tip_cannot_pay_another()
    {
        var card = await SaveCard();
        var bill = await NewBill(4000);
        var small = await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 200 });
        var options = await _rig.Payments().BeginSavedCardPaymentAsync(_pat.UserId, bill.Id, card.Id, small.QuoteId);
        var assertion = _patPhone.Sign(options);

        // Someone with the link swaps in a bigger tip after the fingerprint prompt started.
        var big = await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 1200 });

        var stale = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().PaySavedCardAsync(_pat.UserId, bill.Id,
            new SavedCardPayRequest { SavedCardId = card.Id, QuoteId = small.QuoteId, Challenge = options.Challenge, Assertion = assertion }));
        Assert.Equal(409, stale.StatusCode);

        var swapped = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Payments().PaySavedCardAsync(_pat.UserId, bill.Id,
            new SavedCardPayRequest { SavedCardId = card.Id, QuoteId = big.QuoteId, Challenge = options.Challenge, Assertion = assertion }));
        Assert.Equal(401, swapped.StatusCode);
        Assert.Empty(_rig.Gateway.Charges);
    }

    // Fiserv's Hosted Payment Page --------------------------------------------------------

    [Fact]
    public async Task Hosted_page_opens_with_the_total_and_invoice_filled_in()
    {
        var bill = await NewBill(4000);
        var quote = await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 600 });

        var start = await _rig.Payments().StartHostedPaymentAsync(bill.Id, new HostedPageStartRequest { QuoteId = quote.QuoteId }, ApiBase);

        Assert.Equal($"https://test.securepayments.cardpointe.com/pay?total=46.00&invoice={bill.InvoiceNumber}", start.Url);
        Assert.Equal(4600, start.TotalCents);
        Assert.True((await _rig.Payments().GetPublicAsync(bill.Id)).HostedPageStarted);
        Assert.Empty(_rig.Gateway.Charges); // qrbound never charges for the hosted page
    }

    [Fact]
    public async Task Without_a_hosted_page_the_customer_pays_here()
    {
        _rig.Gateway.HostedPage = null;
        var bill = await NewBill(4000);
        var quote = await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = 0 });

        Assert.False((await _rig.Payments().GetPublicAsync(bill.Id)).HostedPageAvailable);
        var none = await Assert.ThrowsAsync<QrPaymentException>(() =>
            _rig.Payments().StartHostedPaymentAsync(bill.Id, new HostedPageStartRequest { QuoteId = quote.QuoteId }, ApiBase));
        Assert.Equal(409, none.StatusCode);
    }

    [Fact]
    public async Task Hosted_page_payment_is_recorded_once_CardPointe_reports_it()
    {
        var bill = await StartHosted(4000, tip: 600);

        // Nothing at CardPointe yet: still open.
        Assert.Equal(QrPaymentStates.Open, (await _rig.Payments().CheckHostedPaymentAsync(bill.Id)).Status);

        // A decline on the hosted page doesn't count.
        _rig.Gateway.PaidOnHostedPage(bill.InvoiceNumber, 4600, retRef: "HPPDECLINE", respStat: "C");
        Assert.Equal(QrPaymentStates.Open, (await _rig.Payments().CheckHostedPaymentAsync(bill.Id)).Status);

        _rig.Gateway.PaidOnHostedPage(bill.InvoiceNumber, 4600, retRef: "HPP1");
        var info = await _rig.Payments().CheckHostedPaymentAsync(bill.Id);

        Assert.Equal(QrPaymentStates.Paid, info.Status);
        Assert.Equal(600, info.TipCents);
        Assert.Equal(4600, info.PaidAmountCents);
        Assert.Equal(QrPaymentMethods.HostedPage, info.PaymentMethod);
        Assert.Equal("HPP1", info.TransactionId);

        var details = await Details(bill.Id);
        Assert.Equal("Hana Host", details.PayerName);
        Assert.Null(details.LastError);

        // Checking again (or a repeated webhook) changes nothing.
        Assert.True(await _rig.Payments().ConfirmHostedPaymentAsync(bill.InvoiceNumber));
        Assert.Null((await Details(bill.Id)).ExtraTransactionId);
    }

    [Fact]
    public async Task Webhook_only_names_the_invoice_and_unknown_invoices_are_ignored()
    {
        var bill = await StartHosted(4000, tip: 0);
        _rig.Gateway.PaidOnHostedPage(bill.InvoiceNumber, 4000);

        Assert.False(await _rig.Payments().ConfirmHostedPaymentAsync("QR-NOSUCHBILL"));
        Assert.True(await _rig.Payments().ConfirmHostedPaymentAsync(bill.InvoiceNumber));
        Assert.Equal(QrPaymentStates.Paid, (await Details(bill.Id)).Status);
    }

    [Fact]
    public async Task Paying_less_than_the_bill_on_the_hosted_page_needs_review()
    {
        var bill = await StartHosted(4000, tip: 600);
        _rig.Gateway.PaidOnHostedPage(bill.InvoiceNumber, 100); // amount changed to $1.00 on the hosted page

        await _rig.Payments().ConfirmHostedPaymentAsync(bill.InvoiceNumber);

        var details = await Details(bill.Id);
        Assert.Equal(QrPaymentStates.NeedsReview, details.Status);
        Assert.Equal(100, details.PaidAmountCents);
        Assert.Equal(0, details.TipCents);
        Assert.Contains("less than the bill", details.LastError);
    }

    [Fact]
    public async Task Paying_a_different_amount_above_the_bill_counts_the_rest_as_tip_and_says_so()
    {
        var bill = await StartHosted(4000, tip: 600);
        _rig.Gateway.PaidOnHostedPage(bill.InvoiceNumber, 4300);

        await _rig.Payments().ConfirmHostedPaymentAsync(bill.InvoiceNumber);

        var details = await Details(bill.Id);
        Assert.Equal(QrPaymentStates.Paid, details.Status);
        Assert.Equal(300, details.TipCents);
        Assert.Contains("not the $46.00", details.LastError);
    }

    [Fact]
    public async Task A_second_payment_after_the_bill_was_paid_is_flagged_for_refund()
    {
        var bill = await StartHosted(4000, tip: 0);
        await _rig.Payments().PayAsync(bill.Id, Card(null)); // paid here meanwhile, CardPointe RETREF1

        _rig.Gateway.PaidOnHostedPage(bill.InvoiceNumber, 4000, retRef: "RETREF1"); // the same charge, seen by inquiry
        _rig.Gateway.PaidOnHostedPage(bill.InvoiceNumber, 4000, retRef: "HPP2");
        await _rig.Payments().ConfirmHostedPaymentAsync(bill.InvoiceNumber);

        var details = await Details(bill.Id);
        Assert.Equal(QrPaymentStates.Paid, details.Status);
        Assert.Equal("RETREF1", details.TransactionId);
        Assert.Equal("HPP2", details.ExtraTransactionId);
        Assert.Contains("Refund it", details.LastError);
    }

    [Fact]
    public async Task Hosted_page_payment_after_the_link_expired_is_still_recorded()
    {
        var bill = await StartHosted(4000, tip: 0);
        _rig.Clock.Advance(TimeSpan.FromHours(2));
        Assert.Equal(QrPaymentStates.Expired, (await Details(bill.Id)).Status);

        _rig.Gateway.PaidOnHostedPage(bill.InvoiceNumber, 4000);
        await _rig.Payments().CheckWithGatewayAsync(_owner.UserId, _shop.BusinessId, bill.Id);

        Assert.Equal(QrPaymentStates.Paid, (await Details(bill.Id)).Status);
    }

    [Fact]
    public async Task Staff_check_settles_a_timeout_from_CardPointe()
    {
        var bill = await NewBill(4000);
        _rig.Gateway.Next = () => new CardPointeResponse { StatusMessage = "Error", OutcomeUnknown = true, ErrorMessage = "timeout" };
        await _rig.Payments().PayAsync(bill.Id, Card(null));
        Assert.Equal(QrPaymentStates.NeedsReview, (await Details(bill.Id)).Status);

        // CardPointe can't be reached: nothing changes.
        _rig.Gateway.Reachable = false;
        Assert.Equal(QrPaymentStates.NeedsReview, (await _rig.Payments().CheckWithGatewayAsync(_owner.UserId, _shop.BusinessId, bill.Id)).Status);

        _rig.Gateway.Reachable = true;
        _rig.Gateway.PaidOnHostedPage(bill.InvoiceNumber, 4000, retRef: "LATE1");
        var settled = await _rig.Payments().CheckWithGatewayAsync(_owner.UserId, _shop.BusinessId, bill.Id);

        Assert.Equal(QrPaymentStates.Paid, settled.Status);
        Assert.Equal("LATE1", settled.TransactionId);
        Assert.Equal(QrPaymentMethods.Card, settled.PaymentMethod);
    }

    // Settings and report ---------------------------------------------------------------

    [Fact]
    public async Task Tip_settings_are_checked_and_saved()
    {
        var settings = _rig.Settings();
        var bad = await Assert.ThrowsAsync<QrPaymentException>(() => settings.UpdateAsync(_owner.UserId, _shop.BusinessId,
            new UpdateBusinessPaymentSettingsRequest { PaymentsEnabled = true, TipsEnabled = true, TipPercentages = "15, abc", MaxTipPercent = 30 }));
        Assert.Equal(400, bad.StatusCode);

        var overMax = await Assert.ThrowsAsync<QrPaymentException>(() => _rig.Settings().UpdateAsync(_owner.UserId, _shop.BusinessId,
            new UpdateBusinessPaymentSettingsRequest { PaymentsEnabled = true, TipsEnabled = true, TipPercentages = "15, 40", MaxTipPercent = 30 }));
        Assert.Equal(400, overMax.StatusCode);

        var saved = await _rig.Settings().UpdateAsync(_owner.UserId, _shop.BusinessId,
            new UpdateBusinessPaymentSettingsRequest { PaymentsEnabled = true, TipsEnabled = true, TipPercentages = "20, 10%, 15", AllowCustomTip = false, MaxTipPercent = 25 });
        Assert.Equal("10, 15, 20", saved.TipPercentages);
        Assert.False(saved.AllowCustomTip);
        Assert.Equal(25, saved.MaxTipPercent);
    }

    [Fact]
    public async Task Report_splits_bills_tips_and_how_they_were_paid()
    {
        var card = await SaveCard();
        var first = await NewBill(4000);
        var q1 = await _rig.Payments().QuoteAsync(first.Id, new QrPaymentQuoteRequest { TipCents = 600 });
        await _rig.Payments().PayAsync(first.Id, Card(q1.QuoteId));

        var second = await StartHosted(2000, tip: 400);
        _rig.Gateway.PaidOnHostedPage(second.InvoiceNumber, 2400);
        await _rig.Payments().ConfirmHostedPaymentAsync(second.InvoiceNumber);

        var report = await _rig.Settings().ReportAsync(_owner.UserId, _shop.BusinessId, null, null);

        Assert.Equal(2, report.PaidCount);
        Assert.Equal(6000, report.BillTotalCents);
        Assert.Equal(1000, report.TipTotalCents);
        Assert.Equal(7000, report.PaidTotalCents);
        Assert.Equal(1, report.PaidByCardCount);
        Assert.Equal(1, report.PaidOnHostedPageCount);
        Assert.Equal(0, report.PaidBySavedCardCount);
        Assert.Equal(1000, Assert.Single(report.Days).TipTotalCents);
        Assert.NotNull(card);
    }

    // Webhook and gateway parsing -----------------------------------------------------------

    [Theory]
    [InlineData("""{"merchantId":"123","total":"46.00","invoice":"QR-ABC123"}""", "application/json", "QR-ABC123")]
    [InlineData("""{"data":{"transaction":{"orderid":"QR-NESTED"}}}""", "application/json", "QR-NESTED")]
    [InlineData("total=46.00&invoice=QR-FORM1&billFName=Pat", "application/x-www-form-urlencoded", "QR-FORM1")]
    [InlineData("""{"total":"46.00"}""", "application/json", null)]
    [InlineData("""{"invoice":"THIS-IS-FAR-TOO-LONG-FOR-AN-INVOICE"}""", "application/json", null)]
    [InlineData("not json", "text/plain", null)]
    public void Webhook_invoice_is_found(string body, string contentType, string? expected) =>
        Assert.Equal(expected, HostedPageWebhookParser.FindInvoice(body, contentType));

    [Theory]
    [InlineData("46.00", 4600)]
    [InlineData("0.5", 50)]
    [InlineData("4600", 4600)]
    [InlineData("", 0)]
    [InlineData("abc", 0)]
    public void CardPointe_amounts_are_read_as_cents(string amount, long cents) =>
        Assert.Equal(cents, CardPointeService.ParseAmountCents(amount));

    // ---------------------------------------------------------------------------------

    private static readonly Uri ApiBase = new("https://api.test/");

    private async Task<QrPaymentDetails> StartHosted(long cents, long tip)
    {
        var bill = await NewBill(cents);
        var quote = await _rig.Payments().QuoteAsync(bill.Id, new QrPaymentQuoteRequest { TipCents = tip });
        await _rig.Payments().StartHostedPaymentAsync(bill.Id, new HostedPageStartRequest { QuoteId = quote.QuoteId }, ApiBase);
        return bill;
    }

    private async Task UpdateTips(bool tipsEnabled, bool allowCustom) =>
        await _rig.Settings().UpdateAsync(_owner.UserId, _shop.BusinessId, new UpdateBusinessPaymentSettingsRequest
        {
            PaymentsEnabled = true, TipsEnabled = tipsEnabled, TipPercentages = "15, 20", AllowCustomTip = allowCustom, MaxTipPercent = 30
        });

    private Task<QrPaymentDetails> NewBill(long cents) =>
        _rig.Payments().CreateAsync(_owner.UserId, _shop.BusinessId, new CreateQrPaymentRequest { AmountCents = cents, ExpiresInMinutes = 60 });

    private Task<QrPaymentDetails> Details(Guid id) => _rig.Payments().GetAsync(_owner.UserId, _shop.BusinessId, id);

    private static QrPaymentPayRequest Card(Guid? quoteId) =>
        new() { PaymentToken = "9418594164541111", Expiry = "1228", CardholderName = "Pat Payer", QuoteId = quoteId };

    private Task<SavedCardDto> SaveCard() =>
        _rig.SavedCards().AddAsync(_pat.UserId, new SaveCardRequest { PaymentToken = "9418594164541111", Expiry = "1228", CardholderName = "Pat Test" });

    private static User NewUser(string email, string first) =>
        new() { Email = email, FirstName = first, LastName = "Test", PasswordHash = "x", IsActive = true, IsEmailVerified = true };

    public void Dispose() => _patPhone.Dispose();
}
