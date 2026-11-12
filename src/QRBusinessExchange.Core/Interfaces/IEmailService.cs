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

    /// <summary>
    /// Sends a link that lets the user choose a new password
    /// </summary>
    /// <param name="email">The account's email address</param>
    /// <param name="firstName">The user's first name, for the greeting</param>
    /// <param name="resetUrl">The full reset link, including its token</param>
    /// <param name="validFor">How long the link works</param>
    /// <returns>True if email was sent successfully, false otherwise</returns>
    Task<bool> SendPasswordResetEmailAsync(string email, string firstName, string resetUrl, TimeSpan validFor);

    /// <summary>
    /// Tells the user their password was just changed, so they notice if it wasn't them
    /// </summary>
    /// <param name="email">The account's email address</param>
    /// <param name="firstName">The user's first name, for the greeting</param>
    /// <param name="forgotPasswordUrl">Link to request another reset</param>
    /// <returns>True if email was sent successfully, false otherwise</returns>
    Task<bool> SendPasswordChangedEmailAsync(string email, string firstName, string forgotPasswordUrl);
}
