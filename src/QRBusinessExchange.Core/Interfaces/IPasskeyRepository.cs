using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.Core.Interfaces;

/// <summary>
/// Repository interface for passkey credential operations
/// </summary>
public interface IPasskeyRepository
{
    /// <summary>
    /// Get a passkey credential by its ID
    /// </summary>
    Task<PasskeyCredential?> GetByCredentialIdAsync(byte[] credentialId);

    /// <summary>
    /// Get all passkey credentials for a user
    /// </summary>
    Task<List<PasskeyCredential>> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Create a new passkey credential
    /// </summary>
    Task<PasskeyCredential> CreateAsync(PasskeyCredential credential);

    /// <summary>
    /// Update an existing passkey credential
    /// </summary>
    Task<PasskeyCredential> UpdateAsync(PasskeyCredential credential);

    /// <summary>
    /// Delete a passkey credential
    /// </summary>
    Task DeleteAsync(byte[] credentialId);

    /// <summary>
    /// Check if a user has any passkey credentials
    /// </summary>
    Task<bool> UserHasPasskeysAsync(Guid userId);
}
