using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for User entity operations
/// </summary>
public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class
    /// </summary>
    /// <param name="context">The application database context</param>
    public UserRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<User?> GetByIdAsync(Guid userId)
    {
        try
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving user with ID {userId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<User?> GetByEmailAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty", nameof(email));
        }

        try
        {
            return await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email.ToLower());
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving user with email {email}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<User> CreateAsync(User user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        try
        {
            user.CreatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            await _context.Users.AddAsync(user);
            await _context.SaveChangesAsync();

            return user;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Error creating user", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<User> UpdateAsync(User user)
    {
        if (user == null)
        {
            throw new ArgumentNullException(nameof(user));
        }

        try
        {
            var existingUser = await _context.Users.FindAsync(user.UserId);
            if (existingUser == null)
            {
                throw new InvalidOperationException($"User with ID {user.UserId} not found");
            }

            user.UpdatedAt = DateTime.UtcNow;

            _context.Entry(existingUser).CurrentValues.SetValues(user);
            await _context.SaveChangesAsync();

            return existingUser;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Error updating user with ID {user.UserId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DeleteAsync(Guid userId)
    {
        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                return false;
            }

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return true;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Error deleting user with ID {userId}", ex);
        }
    }
}
