using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Data;
using QRCoder;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Service for QR code generation and management using QRCoder library
/// </summary>
public class QRCodeService : IQRCodeService
{
    private readonly ApplicationDbContext _context;
    private readonly string _baseUrl;

    /// <summary>
    /// Initializes a new instance of the <see cref="QRCodeService"/> class
    /// </summary>
    /// <param name="context">The application database context</param>
    /// <param name="baseUrl">The base URL for public profile pages (e.g., "https://qrbusinessexchange.com")</param>
    public QRCodeService(ApplicationDbContext context, string baseUrl = "https://qrbusinessexchange.com")
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));
    }

    /// <inheritdoc/>
    public async Task<Core.Entities.QRCode> GenerateQRCodeAsync(Guid userId)
    {
        try
        {
            // Check if user exists
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User with ID {userId} not found");
            }

            // Check if user already has a QR code
            var existingQRCode = await _context.QRCodes
                .FirstOrDefaultAsync(q => q.UserId == userId);

            if (existingQRCode != null)
            {
                return existingQRCode;
            }

            // Create public profile URL
            var publicProfileUrl = $"{_baseUrl}/profile/{userId}";

            // Generate QR code using QRCoder
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(publicProfileUrl, QRCodeGenerator.ECCLevel.Q);
            using var qrCode = new PngByteQRCode(qrCodeData);
            
            var qrCodeBytes = qrCode.GetGraphic(20); // 20 pixels per module
            var qrCodeBase64 = Convert.ToBase64String(qrCodeBytes);

            // Create QR code entity
            var newQRCode = new Core.Entities.QRCode
            {
                UserId = userId,
                QRCodeData = $"data:image/png;base64,{qrCodeBase64}",
                PublicProfileUrl = publicProfileUrl,
                CreatedAt = DateTime.UtcNow,
                ScansCount = 0
            };

            await _context.QRCodes.AddAsync(newQRCode);
            await _context.SaveChangesAsync();

            return newQRCode;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error generating QR code for user {userId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Core.Entities.QRCode?> GetQRCodeByUserIdAsync(Guid userId)
    {
        try
        {
            return await _context.QRCodes
                .AsNoTracking()
                .Include(q => q.User)
                .FirstOrDefaultAsync(q => q.UserId == userId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving QR code for user {userId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> TrackScanAsync(Guid qrCodeId, Guid? scannedByUserId = null)
    {
        try
        {
            var qrCode = await _context.QRCodes.FindAsync(qrCodeId);
            if (qrCode == null)
            {
                return false;
            }

            // Increment scan count
            qrCode.ScansCount++;

            // Update usage analytics if available
            if (scannedByUserId.HasValue)
            {
                var today = DateTime.UtcNow.Date;
                var analytics = await _context.UsageAnalytics
                    .FirstOrDefaultAsync(ua => ua.UserId == qrCode.UserId && ua.Date == today);

                if (analytics != null)
                {
                    analytics.QRScans++;
                }
                else
                {
                    // Create new analytics record for today
                    var newAnalytics = new UsageAnalytics
                    {
                        UserId = qrCode.UserId,
                        Date = today,
                        QRScans = 1,
                        ConnectionsMade = 0,
                        ProfileViews = 0,
                        MapViews = 0
                    };
                    await _context.UsageAnalytics.AddAsync(newAnalytics);
                }
            }

            await _context.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error tracking scan for QR code {qrCodeId}", ex);
        }
    }
}
