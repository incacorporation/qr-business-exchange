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
    private readonly CardPointeOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<QrPaymentService> _logger;

    public QrPaymentService(
        ApplicationDbContext db,
        ICardPointeService cardPointe,
        SavedCardService savedCards,
        WebAuthnService webAuthn,
        IOptions<CardPointeOptions> options,
        TimeProvider clock,
        ILogger<QrPaymentService> logger)
    {
        _db = db;
        _cardPointe = cardPointe;
        _savedCards = savedCards;
        _webAuthn = webAuthn;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    // Staff ------------------------------------------------------------------

    public async Task<QrPaymentDetails> CreateAsync(Guid userId, Guid businessId, CreateQrPaymentRequest request)
    {
        await RequireStaffAsync(userId, businessId);

        if (request.AmountCents < _options.MinAmountCents || request.AmountCents > _options.MaxAmountCents)
        {
            throw new QrPaymentException(400,
                $"Amount must be between {_options.MinAmountCents / 100m:0.00} and {_options.MaxAmountCents / 100m:0.00} {_options.Currency}.");
        }

        if (request.ExpiresInMinutes is < 5 or > 1440)
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
            ExpiresAt = Now.AddMinutes(request.ExpiresInMinutes)
        };

        _db.QrPaymentRequests.Add(payment);
        await _db.SaveChangesAsync();

        _logger.LogInformation("QR payment {Invoice} for {AmountCents} cents created in business {BusinessId}",
            payment.InvoiceNumber, payment.AmountCents, businessId);

        return ToDetails(payment);
    }

    public async Task<IReadOnlyList<QrPaymentDetails>> ListAsync(Guid userId, Guid businessId, int take = 50)
    {
        await RequireStaffAsync(userId, businessId);

        var payments = await _db.QrPaymentRequests
            .Where(p => p.BusinessId == businessId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(Math.Clamp(take, 1, 200))
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
        await RequireStaffAsync(userId, businessId);
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
        await RequireStaffAsync(userId, businessId);
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

        return new QrPaymentInfo
        {
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
            TransactionId = payment.Status == QrPaymentStatus.Paid ? payment.TransactionId : null
        };
    }

    /// <summary>Charges the payer's card for the request's own amount.</summary>
    public async Task<CardPaymentResult> PayAsync(Guid id, QrPaymentPayRequest request)
    {
        var payment = await LoadPayableAsync(id);
        var email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();

        return await ClaimAndChargeAsync(payment, new CardPointeChargeRequest
        {
            Token = request.PaymentToken,
            Expiry = request.Expiry,
            AmountCents = payment.AmountCents,
            Currency = payment.Currency,
            OrderId = payment.InvoiceNumber,
            CardholderName = request.CardholderName.Trim(),
            Email = email
        }, request.CardholderName.Trim(), email);
    }

    // Saved card + fingerprint ---------------------------------------------------

    /// <summary>
    /// The passkey challenge binds the payment, the card and the amount, so a fingerprint given for
    /// one of them can't be used to approve another.
    /// </summary>
    public static string SavedCardPurpose(Guid paymentId, Guid savedCardId, long amountCents) =>
        $"qr-pay:{paymentId:N}:{savedCardId:N}:{amountCents}";

    /// <summary>
    /// Step 1 of paying with a saved card: checks the request can be paid with this card and returns
    /// the passkey options the phone needs to ask for the user's fingerprint (or face).
    /// </summary>
    public async Task<PasskeyAuthenticationOptionsDto> BeginSavedCardPaymentAsync(Guid userId, Guid paymentId, Guid savedCardId)
    {
        var payment = await LoadPayableAsync(paymentId);
        var card = await _savedCards.GetUsableAsync(userId, savedCardId);
        var user = await RequirePasskeyUserAsync(userId);

        return await _webAuthn.GenerateAuthenticationOptionsAsync(
            user.Email, SavedCardPurpose(payment.QrPaymentRequestId, card.SavedCardId, payment.AmountCents));
    }

    /// <summary>
    /// Step 2: verifies the user's passkey assertion for exactly this payment, card and amount, then
    /// charges the card on file.
    /// </summary>
    public async Task<CardPaymentResult> PaySavedCardAsync(Guid userId, Guid paymentId, SavedCardPayRequest request)
    {
        var payment = await LoadPayableAsync(paymentId);
        var card = await _savedCards.GetUsableAsync(userId, request.SavedCardId);
        var user = await RequirePasskeyUserAsync(userId);

        Guid? signerId;
        try
        {
            (_, signerId) = await _webAuthn.CompleteAuthenticationAsync(
                request.Challenge, request.Assertion,
                SavedCardPurpose(payment.QrPaymentRequestId, card.SavedCardId, payment.AmountCents));
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
            AmountCents = payment.AmountCents,
            Currency = payment.Currency,
            OrderId = payment.InvoiceNumber,
            CardholderName = payerName,
            Email = email
        }, payerName, email);

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

        if (!payment.Business.IsActive)
        {
            throw new QrPaymentException(409, "This business isn't taking payments right now.");
        }

        return payment;
    }

    /// <summary>
    /// Claims the request (Open → Processing), charges it once, and records the outcome.
    /// </summary>
    private async Task<CardPaymentResult> ClaimAndChargeAsync(
        QrPaymentRequest payment, CardPointeChargeRequest charge, string payerName, string? payerEmail)
    {
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
            payment.LastError = null;
        }
        else if (response.OutcomeUnknown)
        {
            payment.Status = QrPaymentStatus.NeedsReview;
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
            AmountCents = payment.AmountCents,
            TransactionDateTime = Now
        };
    }

    // ---------------------------------------------------------------------------

    private async Task RequireStaffAsync(Guid userId, Guid businessId)
    {
        // Any member of the business (or its Administrator, or a SuperAdmin) may take payments.
        if (await BusinessAccess.GetRoleAsync(_db, userId, businessId) is null)
        {
            throw new QrPaymentException(404, "Business not found.");
        }
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
        LastError = p.LastError
    };

    private static string? Truncate(string? text, int max) =>
        text is null || text.Length <= max ? text : text[..max];
}
