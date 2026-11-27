using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Profile pictures (personal photo or business logo): JPEG, PNG or GIF up to <see cref="MaxBytes"/>,
/// stored in the database and served from the API.
/// </summary>
public class ProfileImageService
{
    public const int MaxBytes = 5 * 1024 * 1024;

    private readonly ApplicationDbContext _db;

    public ProfileImageService(ApplicationDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// The image type from the file's first bytes, or null if it isn't a JPEG, PNG or GIF.
    /// The file name and the browser's content type aren't trusted: a renamed HTML or SVG file must
    /// never be stored and served back as a picture.
    /// </summary>
    public static string? DetectContentType(ReadOnlySpan<byte> data)
    {
        if (data.Length >= 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF)
        {
            return "image/jpeg";
        }
        if (data.Length >= 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }))
        {
            return "image/png";
        }
        if (data.Length >= 6 && (data[..6].SequenceEqual("GIF87a"u8) || data[..6].SequenceEqual("GIF89a"u8)))
        {
            return "image/gif";
        }
        return null;
    }

    /// <summary>Saves (or replaces) the user's picture and points their profile at it.</summary>
    /// <param name="imageUrl">The address the picture will be served from (built by the caller, which knows the host).</param>
    public async Task<string> SaveAsync(Guid userId, byte[] data, Func<DateTime, string> imageUrl)
    {
        if (data.Length == 0)
        {
            throw new SsoException("empty_file", "Choose a picture to upload.");
        }
        if (data.Length > MaxBytes)
        {
            throw new SsoException("file_too_large", "The picture is larger than 5 MB. Choose a smaller one.");
        }

        var contentType = DetectContentType(data)
            ?? throw new SsoException("invalid_image", "Only JPG, JPEG, PNG and GIF pictures can be uploaded.");

        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new SsoException("not_found", "Account not found.", 404);

        var now = DateTime.UtcNow;
        var image = await _db.ProfileImages.FirstOrDefaultAsync(i => i.UserId == userId);
        if (image == null)
        {
            _db.ProfileImages.Add(new ProfileImage { UserId = userId, ContentType = contentType, Data = data, UpdatedAt = now });
        }
        else
        {
            image.ContentType = contentType;
            image.Data = data;
            image.UpdatedAt = now;
        }

        // The version in the address makes browsers fetch the new picture instead of a cached old one.
        user.ProfileImageUrl = imageUrl(now);
        user.UpdatedAt = now;
        await _db.SaveChangesAsync();
        return user.ProfileImageUrl;
    }

    /// <summary>Removes the user's picture.</summary>
    public async Task RemoveAsync(Guid userId)
    {
        await _db.ProfileImages.Where(i => i.UserId == userId).ExecuteDeleteAsync();
        await _db.Users.Where(u => u.UserId == userId).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.ProfileImageUrl, (string?)null)
            .SetProperty(u => u.UpdatedAt, DateTime.UtcNow));
    }

    /// <summary>
    /// The picture for anyone to see, or null when there is none or the user hides it from their card.
    /// </summary>
    public Task<(string ContentType, byte[] Data, DateTime UpdatedAt)?> GetPublicAsync(Guid userId) =>
        GetAsync(userId, publicOnly: true);

    /// <summary>The user's own picture (shown to them even when hidden from their card).</summary>
    public Task<(string ContentType, byte[] Data, DateTime UpdatedAt)?> GetOwnAsync(Guid userId) =>
        GetAsync(userId, publicOnly: false);

    private async Task<(string ContentType, byte[] Data, DateTime UpdatedAt)?> GetAsync(Guid userId, bool publicOnly)
    {
        var row = await _db.ProfileImages
            .AsNoTracking()
            .Where(i => i.UserId == userId && i.User.IsActive && (!publicOnly || i.User.ShowProfileImage))
            .Select(i => new { i.ContentType, i.Data, i.UpdatedAt })
            .FirstOrDefaultAsync();

        return row == null ? null : (row.ContentType, row.Data, row.UpdatedAt);
    }
}
