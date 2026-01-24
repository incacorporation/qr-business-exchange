using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.Core.Interfaces;

/// <summary>
/// Service interface for QR code generation and management
/// </summary>
public interface IQRCodeService
{
    /// <summary>
    /// Generates a QR code for a user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <returns>The generated QR code entity with image data</returns>
    Task<QRCode> GenerateQRCodeAsync(Guid userId);

    /// <summary>
    /// Retrieves a user's QR code by their user ID
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <returns>The QR code if found, null otherwise</returns>
    Task<QRCode?> GetQRCodeByUserIdAsync(Guid userId);

    /// <summary>
    /// Tracks a QR code scan event
    /// </summary>
    /// <param name="qrCodeId">The unique identifier of the QR code</param>
    /// <param name="scannedByUserId">The unique identifier of the user who scanned the code (optional)</param>
    /// <returns>True if tracking was successful, false otherwise</returns>
    Task<bool> TrackScanAsync(Guid qrCodeId, Guid? scannedByUserId = null);
}
