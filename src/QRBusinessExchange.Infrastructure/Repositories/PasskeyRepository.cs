using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for passkey credential operations
/// </summary>
public class PasskeyRepository : IPasskeyRepository
{
    private readonly ApplicationDbContext _context;

    public PasskeyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PasskeyCredential?> GetByCredentialIdAsync(byte[] credentialId)
    {
        return await _context.PasskeyCredentials
            .Include(p => p.User)
            .FirstOrDefaultAsync(p => p.CredentialId.SequenceEqual(credentialId));
    }

    public async Task<List<PasskeyCredential>> GetByUserIdAsync(Guid userId)
    {
        return await _context.PasskeyCredentials
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();
    }

    public async Task<PasskeyCredential> CreateAsync(PasskeyCredential credential)
    {
        _context.PasskeyCredentials.Add(credential);
        await _context.SaveChangesAsync();
        return credential;
    }

    public async Task<PasskeyCredential> UpdateAsync(PasskeyCredential credential)
    {
        _context.PasskeyCredentials.Update(credential);
        await _context.SaveChangesAsync();
        return credential;
    }

    public async Task DeleteAsync(byte[] credentialId)
    {
        var credential = await GetByCredentialIdAsync(credentialId);
        if (credential != null)
        {
            _context.PasskeyCredentials.Remove(credential);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> UserHasPasskeysAsync(Guid userId)
    {
        return await _context.PasskeyCredentials
            .AnyAsync(p => p.UserId == userId);
    }
}
