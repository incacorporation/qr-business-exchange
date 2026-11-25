using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.DTOs;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.API.Services;

/// <summary>
/// Cards a user keeps on file for fingerprint checkout. The card is stored in CardPointe's
/// profile service; the app keeps only the profile reference, brand, last four and expiry.
/// </summary>
public sealed class SavedCardService
{
    private readonly ApplicationDbContext _db;
    private readonly ICardPointeService _cardPointe;
    private readonly TimeProvider _clock;
    private readonly ILogger<SavedCardService> _logger;

    public SavedCardService(ApplicationDbContext db, ICardPointeService cardPointe, TimeProvider clock, ILogger<SavedCardService> logger)
    {
        _db = db;
        _cardPointe = cardPointe;
        _clock = clock;
        _logger = logger;
    }

    private DateTime Now => _clock.GetUtcNow().UtcDateTime;

    public async Task<IReadOnlyList<SavedCardDto>> ListAsync(Guid userId)
    {
        var cards = await _db.SavedCards
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.LastUsedAt ?? c.CreatedAt)
            .ToListAsync();
        return cards.Select(ToDto).ToList();
    }

    public async Task<SavedCardDto> AddAsync(Guid userId, SaveCardRequest request)
    {
        if (await _db.SavedCards.CountAsync(c => c.UserId == userId) >= SavedCard.MaxPerUser)
        {
            throw new QrPaymentException(409, $"You can keep up to {SavedCard.MaxPerUser} cards. Remove one first.");
        }

        var probe = new SavedCard { Expiry = request.Expiry };
        if (probe.IsExpiredOn(Now))
        {
            throw new QrPaymentException(400, "This card has expired.");
        }

        var profile = await _cardPointe.CreateProfileAsync(new CardPointeProfileRequest
        {
            Token = request.PaymentToken,
            Expiry = request.Expiry,
            CardholderName = request.CardholderName.Trim()
        });

        if (!profile.IsSuccess)
        {
            throw new QrPaymentException(502, profile.ErrorMessage ?? "The card couldn't be saved.");
        }

        var card = new SavedCard
        {
            UserId = userId,
            ProfileId = profile.ProfileId!,
            AccountId = profile.AccountId!,
            Last4 = profile.Last4 ?? "????",
            CardBrand = profile.CardBrand,
            Expiry = request.Expiry,
            CardholderName = request.CardholderName.Trim(),
            CreatedAt = Now
        };

        _db.SavedCards.Add(card);
        await _db.SaveChangesAsync();

        _logger.LogInformation("User {UserId} saved card ending {Last4}", userId, card.Last4);
        return ToDto(card);
    }

    /// <summary>Removes the card here and from CardPointe.</summary>
    public async Task RemoveAsync(Guid userId, Guid savedCardId)
    {
        var card = await _db.SavedCards.FirstOrDefaultAsync(c => c.SavedCardId == savedCardId && c.UserId == userId)
            ?? throw new QrPaymentException(404, "Card not found.");

        // Forget it here even if CardPointe doesn't answer: the user asked for it gone, and without
        // this row the app can't charge it. A profile CardPointe still holds is logged for cleanup.
        if (!await _cardPointe.DeleteProfileAsync(card.ProfileId, card.AccountId))
        {
            _logger.LogWarning("CardPointe profile {ProfileId}/{AcctId} of removed card {SavedCardId} may still exist; delete it in CardPointe",
                card.ProfileId, card.AccountId, card.SavedCardId);
        }

        _db.SavedCards.Remove(card);
        await _db.SaveChangesAsync();
        _logger.LogInformation("User {UserId} removed card ending {Last4}", userId, card.Last4);
    }

    /// <summary>The user's card, if it's theirs and still valid; for paying with it.</summary>
    public async Task<SavedCard> GetUsableAsync(Guid userId, Guid savedCardId)
    {
        var card = await _db.SavedCards.FirstOrDefaultAsync(c => c.SavedCardId == savedCardId && c.UserId == userId)
            ?? throw new QrPaymentException(404, "Card not found.");

        if (card.IsExpiredOn(Now))
        {
            throw new QrPaymentException(409, "This card has expired. Add a new one.");
        }

        return card;
    }

    private SavedCardDto ToDto(SavedCard c) => new()
    {
        Id = c.SavedCardId,
        Last4 = c.Last4,
        CardBrand = c.CardBrand,
        Expiry = c.Expiry,
        CardholderName = c.CardholderName,
        IsExpired = c.IsExpiredOn(Now),
        CreatedAt = c.CreatedAt,
        LastUsedAt = c.LastUsedAt
    };
}
