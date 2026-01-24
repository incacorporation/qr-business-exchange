using QRBusinessExchange.Core.Entities;

namespace QRBusinessExchange.Core.Interfaces;

/// <summary>
/// Repository interface for Subscription entity operations
/// </summary>
public interface ISubscriptionRepository
{
    /// <summary>
    /// Retrieves a subscription by its unique identifier
    /// </summary>
    /// <param name="subscriptionId">The unique identifier of the subscription</param>
    /// <returns>The subscription if found, null otherwise</returns>
    Task<Subscription?> GetByIdAsync(Guid subscriptionId);

    /// <summary>
    /// Retrieves all subscriptions for a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <returns>A collection of subscriptions for the user</returns>
    Task<IEnumerable<Subscription>> GetByUserIdAsync(Guid userId);

    /// <summary>
    /// Retrieves the active subscription for a specific user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <returns>The active subscription if found, null otherwise</returns>
    Task<Subscription?> GetActiveSubscriptionAsync(Guid userId);

    /// <summary>
    /// Creates a new subscription in the repository
    /// </summary>
    /// <param name="subscription">The subscription entity to create</param>
    /// <returns>The created subscription with generated ID</returns>
    Task<Subscription> CreateAsync(Subscription subscription);

    /// <summary>
    /// Updates an existing subscription in the repository
    /// </summary>
    /// <param name="subscription">The subscription entity to update</param>
    /// <returns>The updated subscription</returns>
    Task<Subscription> UpdateAsync(Subscription subscription);
}
