namespace QRBusinessExchange.Core.Interfaces;

/// <summary>
/// Service interface for email operations
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Sends an email verification message to a user
    /// </summary>
    /// <param name="userId">The unique identifier of the user</param>
    /// <param name="email">The email address to send the verification to</param>
    /// <param name="verificationToken">The verification token to include in the email</param>
    /// <returns>True if email was sent successfully, false otherwise</returns>
    Task<bool> SendVerificationEmailAsync(Guid userId, string email, string verificationToken);

    /// <summary>
    /// Sends a connection request notification email
    /// </summary>
    /// <param name="recipientUserId">The unique identifier of the recipient user</param>
    /// <param name="senderName">The name of the user sending the connection request</param>
    /// <param name="message">Optional message from the sender</param>
    /// <returns>True if email was sent successfully, false otherwise</returns>
    Task<bool> SendConnectionNotificationAsync(Guid recipientUserId, string senderName, string? message = null);
}
