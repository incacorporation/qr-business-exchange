namespace QRBusinessExchange.Core.DTOs;

/// <summary>
/// DTO representing a user's subscription information
/// </summary>
public class SubscriptionDto
{
    /// <summary>
    /// Unique identifier for the subscription
    /// </summary>
    public Guid SubscriptionId { get; set; }

    /// <summary>
    /// Unique identifier of the user
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Type of subscription (Free, Basic, Premium, Enterprise)
    /// </summary>
    public string SubscriptionType { get; set; } = string.Empty;

    /// <summary>
    /// Number of credits available to the user
    /// </summary>
    public int CreditsAvailable { get; set; }

    /// <summary>
    /// Number of credits used by the user
    /// </summary>
    public int CreditsUsed { get; set; }

    /// <summary>
    /// Start date of the subscription
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the subscription (null for active subscriptions without expiry)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Indicates if the subscription is currently active
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Indicates if the subscription will auto-renew
    /// </summary>
    public bool AutoRenew { get; set; }

    /// <summary>
    /// Date and time when the subscription was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
}
