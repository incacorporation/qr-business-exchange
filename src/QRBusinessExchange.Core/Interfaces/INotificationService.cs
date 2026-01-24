namespace QRBusinessExchange.Core.Interfaces;

/// <summary>
/// Service interface for push notification operations
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Sends a push notification to a user
    /// </summary>
    /// <param name="userId">The unique identifier of the user to notify</param>
    /// <param name="title">The notification title</param>
    /// <param name="message">The notification message</param>
    /// <param name="data">Optional additional data to include with the notification</param>
    /// <returns>True if notification was sent successfully, false otherwise</returns>
    Task<bool> SendPushNotificationAsync(Guid userId, string title, string message, Dictionary<string, string>? data = null);

    /// <summary>
    /// Sends a connection request notification to a user
    /// </summary>
    /// <param name="recipientUserId">The unique identifier of the recipient user</param>
    /// <param name="senderUserId">The unique identifier of the sender user</param>
    /// <param name="senderName">The name of the user sending the connection request</param>
    /// <param name="message">Optional message from the sender</param>
    /// <returns>True if notification was sent successfully, false otherwise</returns>
    Task<bool> SendConnectionRequestAsync(Guid recipientUserId, Guid senderUserId, string senderName, string? message = null);
}
