using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Qrbound.Payments.Models;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Data;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Services;

/// <summary>A QR payment rule was broken; StatusCode is the HTTP status to answer with.</summary>
public sealed class QrPaymentException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

/// <summary>
/// QR payment requests: staff of a business create one for an amount and show its QR code; whoever
/// scans it pays that amount by card. Each request is charged at most once: it's claimed
/// (Open → Processing) before CardPointe is called, so two payers can't both charge it, it locks
/// after <see cref="QrPaymentStatus.MaxFailedAttempts"/> declines, and a gateway timeout leaves it in
/// NeedsReview for staff to check in CardPointe rather than charging again.
/// </summary>
public sealed class QrPaymentService
{
    private readonly ApplicationDbContext _db;
    private readonly ICardPointeService _cardPointe;
    private readonly SavedCardService _savedCards;
    private readonly WebAuthnService _webAuthn;
    private readonly BusinessPaymentSettingsService _settings;
    private readonly CardPointeOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<QrPaymentService> _logger;

    public QrPaymentService(
        ApplicationDbContext db,
        ICardPointeService cardPointe,
        SavedCardService savedCards,
        WebAuthnService webAuthn,
        BusinessPaymentSettingsService settings,
        IOptions<CardPointeOptions> options,
        TimeProvider clock,
        ILogger<QrPaymentService> logger)
    {
        _db = db;
        _cardPointe = cardPointe;
        _savedCards = savedCards;
        _webAuthn = webAuthn;
        _settings = settings;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    // Staff ------------------------------------------------------------------

    public async Task<QrPaymentDetails> CreateAsync(Guid userId, Guid businessId, CreateQrPaymentRequest request)
    {
        var settings = await RequirePaymentTakerAsync(userId, businessId);

        if (request.AmountCents < settings.MinAmountCents || request.AmountCents > settings.MaxAmountCents)
        {
            throw new QrPaymentException(400,
                $"This business takes payments between {settings.MinAmountCents / 100m:0.00} and {settings.MaxAmountCents / 100m:0.00} {_options.Currency}.");
        }

        var expiresIn = request.ExpiresInMinutes ?? settings.DefaultExpiresInMinutes;
        if (expiresIn is < 5 or > 1440)
        {
            throw new QrPaymentException(400, "The QR code must stay payable for 5 minutes to 24 hours.");
        }

        var id = Guid.NewGuid();
        var payment = new QrPaymentRequest
        {
            QrPaymentRequestId = id,
            BusinessId = businessId,
            CreatedByUserId = userId,
            AmountCents = request.AmountCents,
            Currency = _options.Currency,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            InvoiceNumber = "QR-" + id.ToString("N")[..12].ToUpperInvariant(),
            Status = QrPaymentStatus.Open,
            CreatedAt = Now,
            ExpiresAt = Now.AddMinutes(expiresIn)
        };

        _db.QrPaymentRequests.Add(payment);
        await _db.SaveChangesAsync();

        _logger.LogInformation("QR payment {Invoice} for {AmountCents} cents created in business {BusinessId}",
            payment.InvoiceNumber, payment.AmountCents, businessId);

        return ToDetails(payment);
    }

    /// <summary>The business's requests, newest first; optionally only those created between two UTC dates (inclusive).</summary>
    public async Task<IReadOnlyList<QrPaymentDetails>> ListAsync(Guid userId, Guid businessId, int take = 50, DateTime? from = null, DateTime? to = null)
    {
        await RequireStaffAsync(userId, businessId);

        var query = _db.QrPaymentRequests.Where(p => p.BusinessId == businessId);
        if (from is { } start)
        {
            query = query.Where(p => p.CreatedAt >= start.Date);
        }

        if (to is { } end)
        {
            query = query.Where(p => p.CreatedAt < end.Date.AddDays(1));
        }

        var payments = await query
            .OrderByDescending(p => p.CreatedAt)
            .Take(Math.Clamp(take, 1, 5000))
            .ToListAsync();

        return payments.Select(ToDetails).ToList();
    }

    public async Task<QrPaymentDetails> GetAsync(Guid userId, Guid businessId, Guid id)
    {
        await RequireStaffAsync(userId, businessId);
        return ToDetails(await FindAsync(businessId, id));
    }

    /// <summary>Stops an Open (or Locked) request from being paid.</summary>
    public async Task<QrPaymentDetails> CancelAsync(Guid userId, Guid businessId, Guid id)
    {
        // Whoever may take payments may cancel them; this works even with payments turned off.
        var role = await RequireStaffAsync(userId, businessId);
        if (!(await _settings.GetEffectiveAsync(businessId)).CanTakePayments(role))
        {
            throw new QrPaymentException(403, "Only the business's Owners and Admins can cancel payments.");
        }

        var payment = await FindAsync(businessId, id);

        if (payment.Status is not (QrPaymentStatus.Open or QrPaymentStatus.Locked))
        {
            throw new QrPaymentException(409, $"A {StateOf(payment)} payment can't be cancelled.");
        }

        payment.Status = QrPaymentStatus.Cancelled;
        await SaveAsync(payment);
        return ToDetails(payment);
    }

    /// <summary>
    /// Settles a NeedsReview request once staff have checked CardPointe: Paid, or back to Open so it can be paid again.
    /// </summary>
    public async Task<QrPaymentDetails> ResolveAsync(Guid userId, Guid businessId, Guid id, ResolveQrPaymentRequest request)
    {
        // Marking money as received (or not) is for the business's Owners and Admins.
        await _settings.RequireManagerAsync(userId, businessId);
        var payment = await FindAsync(businessId, id);

        if (payment.Status != QrPaymentStatus.NeedsReview)
        {
            throw new QrPaymentException(409, "Only a payment waiting for review can be resolved.");
        }

        if (request.Paid)
        {
            payment.Status = QrPaymentStatus.Paid;
            payment.PaidAt = Now;
            payment.TransactionId = string.IsNullOrWhiteSpace(request.TransactionId) ? payment.TransactionId : request.TransactionId.Trim();
            payment.PaidAmountCents ??= payment.AmountCents + payment.TipCents;
        }
        else
        {
            payment.Status = QrPaymentStatus.Open;
            if (payment.ExpiresAt < Now.AddMinutes(10))
            {
                payment.ExpiresAt = Now.AddMinutes(10);
            }
        }

        payment.LastError = null;
        await SaveAsync(payment);

        _logger.LogWarning("QR payment {Invoice} reviewed by {UserId}: {Outcome}", payment.InvoiceNumber, userId, payment.Status);
        return ToDetails(payment);
    }

    // Payer (anyone with the link) ----------------------------------------------

    public async Task<QrPaymentInfo> GetPublicAsync(Guid id)
    {
        var payment = await _db.QrPaymentRequests
            .Include(p => p.Business)
            .FirstOrDefaultAsync(p => p.QrPaymentRequestId == id)
            ?? throw new QrPaymentException(404, "This payment link doesn't exist.");

        return ToInfo(payment, await _settings.GetEffectiveAsync(payment.BusinessId));
    }

    /// <summary>How long a quote (the tip the customer chose) stays locked.</summary>
    public static readonly TimeSpan QuoteLifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Locks the customer's tip into a quote for the bill. Every way of paying names the quote and is
    /// charged exactly its total. A new quote replaces the old one.
    /// </summary>
    public async Task<QrPaymentQuote> QuoteAsync(Guid id, QrPaymentQuoteRequest request)
    {
        var payment = await LoadPayableAsync(id);
        var settings = await _settings.GetEffectiveAsync(payment.BusinessId);

        if (request.TipCents != 0 && !settings.TipsEnabled)
        {
            throw new QrPaymentException(400, "This business doesn't take tips.");
        }

        if (!settings.IsAllowedTip(payment.AmountCents, request.TipCents))
        {
            throw new QrPaymentException(400, request.TipCents > settings.MaxTipCents(payment.AmountCents)
                ? $"The largest tip for this bill is {Money(settings.MaxTipCents(payment.AmountCents), payment.Currency)}."
                : "Choose one of the suggested tips.");
        }

        if (payment.AmountCents + request.TipCents > _options.MaxAmountCents)
        {
            throw new QrPaymentException(400, $"The total can't be more than {Money(_options.MaxAmountCents, payment.Currency)}.");
        }

        payment.QuoteId = Guid.NewGuid();
        payment.QuoteTipCents = request.TipCents;
        payment.QuoteExpiresAt = Now.Add(QuoteLifetime);
        try
        {
            await SaveAsync(payment);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new QrPaymentException(409, "This bill just changed. Please try again.");
        }

        return ToQuote(payment);
    }

    /// <summary>Charges the payer's card (tokenized on the pay page) for the quote's total, or the bill alone.</summary>
    public async Task<CardPaymentResult> PayAsync(Guid id, QrPaymentPayRequest request)
    {
        var payment = await LoadPayableAsync(id);
        var tip = RequireQuote(payment, request.QuoteId);
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        return await ClaimAndChargeAsync(payment, new CardPointeChargeRequest
        {
            Token = request.PaymentToken,
            Expiry = request.Expiry,
            AmountCents = payment.AmountCents + tip,
            Currency = payment.Currency,
            OrderId = payment.InvoiceNumber,
            CardholderName = request.CardholderName.Trim(),
            Email = email
        }, request.CardholderName.Trim(), email, tip, QrPaymentMethods.Card);
    }

    // Fiserv's Hosted Payment Page -------------------------------------------------

    /// <summary>
    /// Sends the customer to Fiserv's CardPointe Hosted Payment Page for the quote's total. The
    /// customer submits the payment to Fiserv there; qrbound only learns of it from CardPointe (the
    /// hosted page's webhook, or the pay page asking), and checks it before marking the bill paid.
    /// </summary>
    public async Task<HostedPageStart> StartHostedPaymentAsync(Guid id, HostedPageStartRequest request, Uri apiBase)
    {
        var payment = await LoadPayableAsync(id);
        var tip = RequireQuote(payment, request.QuoteId);
        var page = _cardPointe.GetHostedPageUrl(apiBase)
            ?? throw new QrPaymentException(409, "Paying on the secure card page isn't available. Pay with a card here instead.");

        payment.HostedStartedAt = Now;
        try
        {
            await SaveAsync(payment);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new QrPaymentException(409, "This bill just changed. Please try again.");
        }

        var total = payment.AmountCents + tip;

        // The hosted page fills these in. "invoice" reaches the gateway as the order ID, which is how
        // the payment is matched back to this bill. The customer can still change the amount there, so
        // what they actually paid is checked when the payment comes back.
        var query = $"total={Uri.EscapeDataString((total / 100m).ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))}" +
                    $"&invoice={Uri.EscapeDataString(payment.InvoiceNumber)}";
        var url = new UriBuilder(page) { Query = string.IsNullOrEmpty(page.Query) ? query : page.Query.TrimStart('?') + "&" + query };

        _logger.LogInformation("QR payment {Invoice}: customer sent to the hosted payment page for {TotalCents} cents (tip {TipCents})",
            payment.InvoiceNumber, total, tip);

        return new HostedPageStart { Url = url.Uri.AbsoluteUri, TotalCents = total, InvoiceNumber = payment.InvoiceNumber };
    }

    /// <summary>
    /// The pay page asks whether the customer has paid on the hosted page yet: looks the bill's order
    /// up with CardPointe and records any approved payment. Returns the bill as the customer sees it.
    /// </summary>
    public async Task<QrPaymentInfo> CheckHostedPaymentAsync(Guid id)
    {
        var payment = await _db.QrPaymentRequests
            .Include(p => p.Business)
            .FirstOrDefaultAsync(p => p.QrPaymentRequestId == id)
            ?? throw new QrPaymentException(404, "This payment link doesn't exist.");

        if (payment.HostedStartedAt != null)
        {
            payment = await ConfirmWithGatewayAsync(payment);
        }

        return ToInfo(payment, await _settings.GetEffectiveAsync(payment.BusinessId));
    }

    /// <summary>
    /// The hosted page's webhook named this invoice number. The webhook itself isn't trusted: the order
    /// is looked up with CardPointe. Returns false when no bill has that invoice number.
    /// </summary>
    public async Task<bool> ConfirmHostedPaymentAsync(string invoiceNumber)
    {
        var invoice = invoiceNumber.Trim();
        var payment = await _db.QrPaymentRequests.FirstOrDefaultAsync(p => p.InvoiceNumber == invoice);
        if (payment == null)
        {
            _logger.LogWarning("Hosted payment page webhook for unknown invoice {Invoice}", invoice);
            return false;
        }

        await ConfirmWithGatewayAsync(payment);
        return true;
    }

    /// <summary>
    /// Staff: asks CardPointe about the bill's order (after a hosted page payment or a gateway timeout)
    /// and records any approved payment it finds.
    /// </summary>
    public async Task<QrPaymentDetails> CheckWithGatewayAsync(Guid userId, Guid businessId, Guid id)
    {
        await RequireStaffAsync(userId, businessId);
        var payment = await FindAsync(businessId, id);
        return ToDetails(await ConfirmWithGatewayAsync(payment));
    }

    /// <summary>
    /// Records approved CardPointe transactions for the bill's order ID that qrbound doesn't know yet.
    /// The first one pays an open bill: at least the bill is required, anything above it is the tip.
    /// Any later one is kept as an extra charge for staff to refund.
    /// </summary>
    private async Task<QrPaymentRequest> ConfirmWithGatewayAsync(QrPaymentRequest payment)
    {
        // A charge sent from the pay page is in flight; its own outcome is recorded when it returns.
        if (payment.Status == QrPaymentStatus.Processing)
        {
            return payment;
        }

        var inquiry = await _cardPointe.InquireByOrderIdAsync(payment.InvoiceNumber, CancellationToken.None);
        if (!inquiry.Reached)
        {
            return payment;
        }

        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (payment.TransactionId != null) known.Add(payment.TransactionId);
        if (payment.ExtraTransactionId != null) known.Add(payment.ExtraTransactionId);

        var fresh = inquiry.Transactions.Where(t => t.IsApproved && !known.Contains(t.RetRef)).ToList();
        if (fresh.Count == 0)
        {
            return payment;
        }

        foreach (var transaction in fresh)
        {
            RecordGatewayPayment(payment, transaction);
        }

        try
        {
            await SaveAsync(payment);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Something else changed the bill at the same moment; the next check records it.
            _logger.LogWarning("QR payment {Invoice}: changed while recording a hosted page payment; will retry on the next check", payment.InvoiceNumber);
            await _db.Entry(payment).ReloadAsync();
        }

        return payment;
    }

    private void RecordGatewayPayment(QrPaymentRequest payment, CardPointeTransaction transaction)
    {
        // A timeout left this in review without a transaction: this approved charge is its outcome.
        var awaitingOutcome = payment.Status == QrPaymentStatus.NeedsReview && payment.TransactionId == null;
        var payable = payment.Status is QrPaymentStatus.Open or QrPaymentStatus.Locked || awaitingOutcome;

        if (!payable)
        {
            payment.ExtraTransactionId ??= transaction.RetRef;
            payment.LastError = Truncate(
                $"A second payment of {Money(transaction.AmountCents, payment.Currency)} came in (CardPointe {transaction.RetRef}). Refund it in CardPointe.", 250);
            _logger.LogWarning("QR payment {Invoice} ({Status}): extra approved charge {RetRef} for {AmountCents} cents needs a refund",
                payment.InvoiceNumber, payment.Status, transaction.RetRef, transaction.AmountCents);
            return;
        }

        payment.TransactionId = transaction.RetRef;
        payment.AuthorizationCode = transaction.AuthCode;
        payment.PaidAmountCents = transaction.AmountCents;
        payment.PayerName = Truncate(transaction.Name, 100) ?? payment.PayerName;
        payment.PaymentMethod = awaitingOutcome ? payment.PaymentMethod : QrPaymentMethods.HostedPage;

        if (transaction.AmountCents < payment.AmountCents)
        {
            // The amount was changed on the hosted page. The money is in, but the bill isn't covered.
            payment.Status = QrPaymentStatus.NeedsReview;
            payment.TipCents = 0;
            payment.LastError = Truncate(
                $"Paid {Money(transaction.AmountCents, payment.Currency)}, less than the bill of {Money(payment.AmountCents, payment.Currency)}. " +
                "Void or refund it in CardPointe, or collect the rest, then resolve this.", 250);
            _logger.LogWarning("QR payment {Invoice}: underpaid on the hosted page ({Paid} of {Bill} cents)",
                payment.InvoiceNumber, transaction.AmountCents, payment.AmountCents);
            return;
        }

        payment.Status = QrPaymentStatus.Paid;
        payment.PaidAt = Now;
        payment.TipCents = transaction.AmountCents - payment.AmountCents;

        var quoted = payment.AmountCents + (payment.QuoteTipCents ?? 0);
        payment.LastError = transaction.AmountCents == quoted || awaitingOutcome
            ? null
            : Truncate($"Paid {Money(transaction.AmountCents, payment.Currency)}, not the {Money(quoted, payment.Currency)} the customer chose; the difference is counted as tip.", 250);

        _logger.LogInformation("QR payment {Invoice}: paid {AmountCents} cents (tip {TipCents}) per CardPointe {RetRef}",
            payment.InvoiceNumber, transaction.AmountCents, payment.TipCents, transaction.RetRef);
    }

    // Saved card + fingerprint ---------------------------------------------------

    /// <summary>
    /// The passkey challenge binds the payment, the card, the quote and the total, so a fingerprint
    /// given for one of them can't approve another, and can't approve a different tip.
    /// </summary>
    public static string SavedCardPurpose(Guid paymentId, Guid savedCardId, Guid? quoteId, long totalCents) =>
        $"qr-pay:{paymentId:N}:{savedCardId:N}:{(quoteId is { } q ? q.ToString("N") : "-")}:{totalCents}";

    /// <summary>
    /// Step 1 of paying with a saved card: checks the request can be paid with this card and returns
    /// the passkey options the phone needs to ask for the user's fingerprint (or face) to approve the
    /// quote's total (or the bill alone, without a quote).
    /// </summary>
    public async Task<PasskeyAuthenticationOptionsDto> BeginSavedCardPaymentAsync(Guid userId, Guid paymentId, Guid savedCardId, Guid? quoteId = null)
    {
        var payment = await LoadPayableAsync(paymentId);
        await RequireSavedCardsAllowedAsync(payment);
        var tip = RequireQuote(payment, quoteId);
        var card = await _savedCards.GetUsableAsync(userId, savedCardId);
        var user = await RequirePasskeyUserAsync(userId);

        return await _webAuthn.GenerateAuthenticationOptionsAsync(
            user.Email, SavedCardPurpose(payment.QrPaymentRequestId, card.SavedCardId, quoteId, payment.AmountCents + tip));
    }

    /// <summary>
    /// Step 2: verifies the user's passkey assertion for exactly this payment, card, quote and total,
    /// then charges the card on file that total.
    /// </summary>
    public async Task<CardPaymentResult> PaySavedCardAsync(Guid userId, Guid paymentId, SavedCardPayRequest request)
    {
        var payment = await LoadPayableAsync(paymentId);
        await RequireSavedCardsAllowedAsync(payment);
        var tip = RequireQuote(payment, request.QuoteId);
        var card = await _savedCards.GetUsableAsync(userId, request.SavedCardId);
        var user = await RequirePasskeyUserAsync(userId);
        var total = payment.AmountCents + tip;

        Guid? signerId;
        try
        {
            (_, signerId) = await _webAuthn.CompleteAuthenticationAsync(
                request.Challenge, request.Assertion,
                SavedCardPurpose(payment.QrPaymentRequestId, card.SavedCardId, request.QuoteId, total));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Fingerprint confirmation failed for QR payment {Invoice} by user {UserId}", payment.InvoiceNumber, userId);
            throw new QrPaymentException(401, "Your fingerprint confirmation didn't go through. Please try again.");
        }

        if (signerId != userId)
        {
            _logger.LogWarning("QR payment {Invoice}: passkey of user {SignerId} used by user {UserId}", payment.InvoiceNumber, signerId, userId);
            throw new QrPaymentException(401, "Confirm with a passkey from your own account.");
        }

        var email = string.IsNullOrWhiteSpace(request.Email) ? user.Email : request.Email.Trim();
        var payerName = card.CardholderName ?? $"{user.FirstName} {user.LastName}".Trim();

        var result = await ClaimAndChargeAsync(payment, new CardPointeChargeRequest
        {
            ProfileId = card.ProfileId,
            AccountId = card.AccountId,
            AmountCents = total,
            Currency = payment.Currency,
            OrderId = payment.InvoiceNumber,
            CardholderName = payerName,
            Email = email
        }, payerName, email, tip, QrPaymentMethods.SavedCard);

        if (result.IsSuccess)
        {
            card.LastUsedAt = Now;
            await _db.SaveChangesAsync();
        }

        return result;
    }

    private async Task<User> RequirePasskeyUserAsync(Guid userId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new QrPaymentException(401, "Sign in again to pay with a saved card.");

        if (!await _db.PasskeyCredentials.AnyAsync(c => c.UserId == userId))
        {
            throw new QrPaymentException(409, "Add a passkey to your account to pay with your fingerprint.");
        }

        return user;
    }

    // ---------------------------------------------------------------------------

    /// <summary>Loads a request that can be paid right now, or says why it can't.</summary>
    private async Task<QrPaymentRequest> LoadPayableAsync(Guid id)
    {
        var payment = await _db.QrPaymentRequests
            .Include(p => p.Business)
            .FirstOrDefaultAsync(p => p.QrPaymentRequestId == id)
            ?? throw new QrPaymentException(404, "This payment link doesn't exist.");

        var state = StateOf(payment);
        if (state != QrPaymentStatus.Open)
        {
            throw new QrPaymentException(409, state switch
            {
                QrPaymentStatus.Paid => "This has already been paid.",
                QrPaymentStates.Expired => "This payment link has expired. Ask for a new QR code.",
                QrPaymentStatus.Processing => "A payment for this is already in progress.",
                QrPaymentStatus.NeedsReview => "We're checking an earlier payment attempt. Please ask staff before paying again.",
                QrPaymentStatus.Locked => "Too many card attempts. Ask for a new QR code.",
                _ => "This payment link is no longer active."
            });
        }

        if (!payment.Business.IsActive || !(await _settings.GetEffectiveAsync(payment.BusinessId)).PaymentsEnabled)
        {
            throw new QrPaymentException(409, "This business isn't taking payments right now.");
        }

        return payment;
    }

    /// <summary>
    /// Claims the request (Open → Processing), charges it once, and records the outcome.
    /// </summary>
    private async Task<CardPaymentResult> ClaimAndChargeAsync(
        QrPaymentRequest payment, CardPointeChargeRequest charge, string payerName, string? payerEmail, long tipCents, string method)
    {
        if (charge.AmountCents != payment.AmountCents + tipCents)
        {
            throw new InvalidOperationException("The charge must be the bill plus the tip.");
        }

        // Claim it: if another payer got here first, the concurrency stamp no longer matches.
        payment.Status = QrPaymentStatus.Processing;
        try
        {
            await SaveAsync(payment);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new QrPaymentException(409, "A payment for this is already in progress.");
        }

        CardPointeResponse response;
        try
        {
            // Not the request's token: once the charge is sent, record its outcome even if the payer leaves.
            response = await _cardPointe.ChargeAsync(charge, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Charging QR payment {Invoice} failed unexpectedly; marked for review", payment.InvoiceNumber);
            response = new CardPointeResponse
            {
                StatusMessage = "Error",
                OutcomeUnknown = true,
                ErrorMessage = "We couldn't confirm the payment. Please ask staff before trying again."
            };
        }

        if (response.IsSuccess)
        {
            payment.Status = QrPaymentStatus.Paid;
            payment.PaidAt = Now;
            payment.TransactionId = response.TransactionId;
            payment.AuthorizationCode = response.AuthorizationCode;
            payment.PayerName = Truncate(payerName, 100);
            payment.PayerEmail = payerEmail;
            payment.TipCents = tipCents;
            payment.PaidAmountCents = charge.AmountCents;
            payment.PaymentMethod = method;
            payment.LastError = null;
        }
        else if (response.OutcomeUnknown)
        {
            payment.Status = QrPaymentStatus.NeedsReview;
            payment.PaymentMethod = method;
            payment.PayerName = Truncate(payerName, 100);
            payment.PayerEmail = payerEmail;
            payment.LastError = Truncate(response.ErrorMessage, 250);
        }
        else
        {
            payment.FailedAttempts++;
            payment.LastError = Truncate(response.ErrorMessage ?? response.StatusMessage, 250);
            payment.Status = payment.FailedAttempts >= QrPaymentStatus.MaxFailedAttempts
                ? QrPaymentStatus.Locked
                : QrPaymentStatus.Open;
        }

        await SaveAsync(payment);

        _logger.LogInformation("QR payment {Invoice}: {Status} ({Attempts} failed attempts)",
            payment.InvoiceNumber, payment.Status, payment.FailedAttempts);

        var message = response.ErrorMessage;
        if (payment.Status == QrPaymentStatus.Locked)
        {
            message = "The card was declined, and this payment link is now locked after too many attempts. Ask for a new QR code.";
        }

        return new CardPaymentResult
        {
            IsSuccess = response.IsSuccess,
            TransactionId = response.TransactionId,
            AuthorizationCode = response.AuthorizationCode,
            StatusMessage = response.StatusMessage,
            ErrorMessage = response.IsSuccess ? null : message,
            AmountCents = charge.AmountCents,
            TransactionDateTime = Now
        };
    }

    // ---------------------------------------------------------------------------

    /// <summary>Any member of the business (or its Administrator, or a SuperAdmin) can see its payments.</summary>
    private Task<string> RequireStaffAsync(Guid userId, Guid businessId) =>
        _settings.RequireMemberAsync(userId, businessId);

    /// <summary>
    /// The business has payments turned on and this user may take them (any member, or only Owners and
    /// Admins, as the business chose). Returns the business's settings.
    /// </summary>
    private async Task<EffectivePaymentSettings> RequirePaymentTakerAsync(Guid userId, Guid businessId)
    {
        var role = await RequireStaffAsync(userId, businessId);
        var settings = await _settings.GetEffectiveAsync(businessId);

        if (!settings.PaymentsEnabled)
        {
            throw new QrPaymentException(409,
                BusinessRoles.CanManage(role)
                    ? "Payments are turned off for this business. Turn them on in Payment settings."
                    : "Payments are turned off for this business. Ask an Owner or Admin to turn them on.");
        }

        if (!settings.CanTakePayments(role))
        {
            throw new QrPaymentException(403, "In this business only Owners and Admins can take payments.");
        }

        return settings;
    }

    /// <summary>Fingerprint checkout must be allowed by the business that's being paid.</summary>
    private async Task RequireSavedCardsAllowedAsync(QrPaymentRequest payment)
    {
        if (!(await _settings.GetEffectiveAsync(payment.BusinessId)).AllowSavedCards)
        {
            throw new QrPaymentException(409, "This business doesn't accept saved cards. Pay with a card instead.");
        }
    }

    /// <summary>
    /// The tip to charge: the current quote's, or none without a quote. A replaced or expired quote is
    /// refused, so the customer is never charged a total they didn't see last.
    /// </summary>
    private long RequireQuote(QrPaymentRequest payment, Guid? quoteId)
    {
        if (quoteId is null)
        {
            return 0;
        }

        if (payment.QuoteId != quoteId || payment.QuoteTipCents is null)
        {
            throw new QrPaymentException(409, "The total changed. Check it and try again.");
        }

        if (payment.QuoteExpiresAt is not { } expires || expires <= Now)
        {
            throw new QrPaymentException(409, "That total has timed out. Check it and try again.");
        }

        return payment.QuoteTipCents.Value;
    }

    private QrPaymentQuote ToQuote(QrPaymentRequest p) => new()
    {
        QuoteId = p.QuoteId!.Value,
        PaymentId = p.QrPaymentRequestId,
        AmountCents = p.AmountCents,
        TipCents = p.QuoteTipCents ?? 0,
        TotalCents = p.AmountCents + (p.QuoteTipCents ?? 0),
        Currency = p.Currency,
        ExpiresAt = p.QuoteExpiresAt!.Value
    };

    /// <summary>Any absolute URL: availability of the hosted page doesn't depend on the API's address.</summary>
    private static readonly Uri AnyBase = new("https://localhost/");

    private QrPaymentInfo ToInfo(QrPaymentRequest payment, EffectivePaymentSettings settings)
    {
        var paid = payment.Status == QrPaymentStatus.Paid;
        return new QrPaymentInfo
        {
            SavedCardsAccepted = settings.AllowSavedCards,
            TipsEnabled = settings.TipsEnabled,
            TipPercentages = settings.TipsEnabled ? settings.TipPercentages.ToList() : new List<int>(),
            AllowCustomTip = settings.TipsEnabled && settings.AllowCustomTip,
            MaxTipPercent = settings.TipsEnabled ? settings.MaxTipPercent : 0,
            HostedPageAvailable = _cardPointe.GetHostedPageUrl(AnyBase) != null,
            HostedPageStarted = payment.HostedStartedAt != null,
            Id = payment.QrPaymentRequestId,
            BusinessName = payment.Business.Name,
            BusinessLogoUrl = payment.Business.LogoUrl,
            AmountCents = payment.AmountCents,
            Currency = payment.Currency,
            Description = payment.Description,
            InvoiceNumber = payment.InvoiceNumber,
            Status = StateOf(payment),
            ExpiresAt = payment.ExpiresAt,
            PaidAt = payment.PaidAt,
            TransactionId = paid ? payment.TransactionId : null,
            TipCents = paid ? payment.TipCents : 0,
            PaidAmountCents = paid ? payment.PaidAmountCents ?? payment.AmountCents : null,
            PaymentMethod = paid ? payment.PaymentMethod : null
        };
    }

    private static string Money(long cents, string currency)
    {
        var amount = (cents / 100m).ToString("N2", System.Globalization.CultureInfo.InvariantCulture);
        return currency == "USD" ? "$" + amount : amount + " " + currency;
    }

    private async Task<QrPaymentRequest> FindAsync(Guid businessId, Guid id) =>
        await _db.QrPaymentRequests.FirstOrDefaultAsync(p => p.QrPaymentRequestId == id && p.BusinessId == businessId)
        ?? throw new QrPaymentException(404, "Payment not found.");

    private Task SaveAsync(QrPaymentRequest payment)
    {
        payment.ConcurrencyStamp = Guid.NewGuid();
        return _db.SaveChangesAsync();
    }

    /// <summary>The stored status, except an Open request past its expiry reads as Expired.</summary>
    private string StateOf(QrPaymentRequest payment) =>
        payment.Status == QrPaymentStatus.Open && payment.ExpiresAt <= Now ? QrPaymentStates.Expired : payment.Status;

    private QrPaymentDetails ToDetails(QrPaymentRequest p) => new()
    {
        Id = p.QrPaymentRequestId,
        BusinessId = p.BusinessId,
        AmountCents = p.AmountCents,
        Currency = p.Currency,
        Description = p.Description,
        InvoiceNumber = p.InvoiceNumber,
        Status = StateOf(p),
        CreatedAt = p.CreatedAt,
        ExpiresAt = p.ExpiresAt,
        PaidAt = p.PaidAt,
        TransactionId = p.TransactionId,
        AuthorizationCode = p.AuthorizationCode,
        PayerName = p.PayerName,
        PayerEmail = p.PayerEmail,
        FailedAttempts = p.FailedAttempts,
        LastError = p.LastError,
        TipCents = p.TipCents,
        PaidAmountCents = p.PaidAmountCents,
        PaymentMethod = p.PaymentMethod,
        HostedStartedAt = p.HostedStartedAt,
        ExtraTransactionId = p.ExtraTransactionId
    };

    private static string? Truncate(string? text, int max) =>
        text is null || text.Length <= max ? text : text[..max];
}
