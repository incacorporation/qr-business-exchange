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

    /// <summary>
    /// Tells an Owner or Admin of an application's business that someone asked to use the
    /// application as one of its categories, with a link to review the request.
    /// </summary>
    Task<bool> SendSsoAccessRequestedEmailAsync(string email, string firstName, string requesterName,
        string requesterEmail, string applicationName, string categoryName, string? actingBusinessName,
        string? note, string reviewUrl) => Task.FromResult(false);

    /// <summary>
    /// Tells a user that their request to use an application as a category was approved or
    /// rejected, or that their access was revoked.
    /// </summary>
    /// <param name="status">Approved, Rejected or Revoked</param>
    /// <param name="applicationUrl">Where to sign in after approval, if known</param>
    Task<bool> SendSsoAccessDecisionEmailAsync(string email, string firstName, string applicationName,
        string categoryName, string status, string? note, string? applicationUrl) => Task.FromResult(false);

    /// <summary>
    /// Sends a short test email straight away, for diagnosing the mail setup.
    /// </summary>
    /// <returns>Null if the mail server accepted it, otherwise why it failed (never includes the password)</returns>
    Task<string?> SendTestEmailAsync(string to) => Task.FromResult<string?>("Test emails aren't supported by this email service.");
}
