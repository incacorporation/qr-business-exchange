using Microsoft.EntityFrameworkCore;
using QRBusinessExchange.Core.Entities;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Subscription entity operations
/// </summary>
public class SubscriptionRepository : ISubscriptionRepository
{
    private readonly ApplicationDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionRepository"/> class
    /// </summary>
    /// <param name="context">The application database context</param>
    public SubscriptionRepository(ApplicationDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <inheritdoc/>
    public async Task<Subscription?> GetByIdAsync(Guid subscriptionId)
    {
        try
        {
            return await _context.Subscriptions
                .AsNoTracking()
                .Include(s => s.User)
                .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving subscription with ID {subscriptionId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<Subscription>> GetByUserIdAsync(Guid userId)
    {
        try
        {
            return await _context.Subscriptions
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving subscriptions for user {userId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Subscription?> GetActiveSubscriptionAsync(Guid userId)
    {
        try
        {
            var now = DateTime.UtcNow;
            return await _context.Subscriptions
                .AsNoTracking()
                .Where(s => s.UserId == userId 
                    && s.IsActive 
                    && s.StartDate <= now 
                    && (s.EndDate == null || s.EndDate > now))
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error retrieving active subscription for user {userId}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Subscription> CreateAsync(Subscription subscription)
    {
        if (subscription == null)
        {
            throw new ArgumentNullException(nameof(subscription));
        }

        try
        {
            subscription.CreatedAt = DateTime.UtcNow;

            await _context.Subscriptions.AddAsync(subscription);
            await _context.SaveChangesAsync();

            return subscription;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Error creating subscription", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Subscription> UpdateAsync(Subscription subscription)
    {
        if (subscription == null)
        {
            throw new ArgumentNullException(nameof(subscription));
        }

        try
        {
            var existingSubscription = await _context.Subscriptions.FindAsync(subscription.SubscriptionId);
            if (existingSubscription == null)
            {
                throw new InvalidOperationException($"Subscription with ID {subscription.SubscriptionId} not found");
            }

            _context.Entry(existingSubscription).CurrentValues.SetValues(subscription);
            await _context.SaveChangesAsync();

            return existingSubscription;
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException($"Error updating subscription with ID {subscription.SubscriptionId}", ex);
        }
    }
}
