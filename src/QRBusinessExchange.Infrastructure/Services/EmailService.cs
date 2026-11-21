using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using QRBusinessExchange.Core.Interfaces;
using QRBusinessExchange.Infrastructure.Configuration;
using QRBusinessExchange.Infrastructure.Data;

namespace QRBusinessExchange.Infrastructure.Services;

/// <summary>
/// Service for sending emails using SMTP
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailSettings _emailSettings;
    private readonly ApplicationDbContext _context;
    private readonly string _baseUrl;
    private readonly ILogger<EmailService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailService"/> class
    /// </summary>
    /// <param name="emailSettings">Email configuration settings</param>
    /// <param name="context">The application database context</param>
    /// <param name="logger">Logger instance for diagnostics</param>
    /// <param name="baseUrl">The base URL for verification links (e.g., "https://qrbusinessexchange.com")</param>
    public EmailService(IOptions<EmailSettings> emailSettings, ApplicationDbContext context, ILogger<EmailService> logger, string baseUrl = "https://qrbusinessexchange.com")
    {
        _emailSettings = emailSettings?.Value ?? throw new ArgumentNullException(nameof(emailSettings));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));

        // Don't validate on construction - validate when actually sending email
        // This allows the app to start even if SMTP is not configured
        // ValidateSettings();
    }

    /// <inheritdoc/>
    public async Task<bool> SendVerificationEmailAsync(Guid userId, string email, string verificationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty", nameof(email));
        }

        if (string.IsNullOrWhiteSpace(verificationToken))
        {
            throw new ArgumentException("Verification token cannot be null or empty", nameof(verificationToken));
        }

        try
        {
            var user = await _context.Users.FindAsync(userId);
            if (user == null)
            {
                throw new InvalidOperationException($"User with ID {userId} not found");
            }

            // Token in the fragment: the portal page reads it in the browser, so it never appears in a
            // request line, server log or Referer header (same as password-reset links).
            var verificationUrl = $"{_baseUrl.TrimEnd('/')}/verify-email#token={Uri.EscapeDataString(verificationToken)}";

            var subject = "Verify Your Email - QR Business Exchange";
            var body = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
                        .content {{ background-color: #f9f9f9; padding: 30px; }}
                        .button {{ display: inline-block; padding: 12px 30px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>QR Business Exchange</h1>
                        </div>
                        <div class='content'>
                            <h2>Welcome, {user.FirstName}!</h2>
                            <p>Thank you for registering with QR Business Exchange. To complete your registration, please verify your email address by clicking the button below:</p>
                            <p style='text-align: center;'>
                                <a href='{verificationUrl}' class='button'>Verify Email Address</a>
                            </p>
                            <p>Or copy and paste this link into your browser:</p>
                            <p style='word-break: break-all;'>{verificationUrl}</p>
                            <p>This verification link will expire in 24 hours.</p>
                            <p>If you did not create an account, please ignore this email.</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; {DateTime.UtcNow.Year} QR Business Exchange. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>
            ";

            return await SendEmailAsync(email, subject, body);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error sending verification email to {email}", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<bool> SendConnectionNotificationAsync(Guid recipientUserId, string senderName, string? message = null)
    {
        if (string.IsNullOrWhiteSpace(senderName))
        {
            throw new ArgumentException("Sender name cannot be null or empty", nameof(senderName));
        }

        try
        {
            var recipient = await _context.Users.FindAsync(recipientUserId);
            if (recipient == null)
            {
                throw new InvalidOperationException($"Recipient user with ID {recipientUserId} not found");
            }

            var subject = $"New Connection Request from {senderName}";
            var messageSection = !string.IsNullOrWhiteSpace(message)
                ? $"<p><strong>Message:</strong> {message}</p>"
                : "";

            var body = $@"
                <html>
                <head>
                    <style>
                        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
                        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
                        .header {{ background-color: #007bff; color: white; padding: 20px; text-align: center; }}
                        .content {{ background-color: #f9f9f9; padding: 30px; }}
                        .button {{ display: inline-block; padding: 12px 30px; background-color: #007bff; color: white; text-decoration: none; border-radius: 5px; margin: 20px 0; }}
                        .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #666; }}
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='header'>
                            <h1>QR Business Exchange</h1>
                        </div>
                        <div class='content'>
                            <h2>New Connection Request</h2>
                            <p>Hello {recipient.FirstName},</p>
                            <p><strong>{senderName}</strong> has sent you a connection request on QR Business Exchange.</p>
                            {messageSection}
                            <p style='text-align: center;'>
                                <a href='{_baseUrl}/connections' class='button'>View Connection Request</a>
                            </p>
                            <p>Log in to your account to accept or decline this connection request.</p>
                        </div>
                        <div class='footer'>
                            <p>&copy; {DateTime.UtcNow.Year} QR Business Exchange. All rights reserved.</p>
                        </div>
                    </div>
                </body>
                </html>
            ";

            return await SendEmailAsync(recipient.Email, subject, body);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Error sending connection notification to user {recipientUserId}", ex);
        }
    }

    /// <inheritdoc/>
    public Task<bool> SendPasswordResetEmailAsync(string email, string firstName, string resetUrl, TimeSpan validFor)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty", nameof(email));
        }

        var url = WebUtility.HtmlEncode(resetUrl);
        var minutes = (int)Math.Round(validFor.TotalMinutes);
        var body = BrandedEmail(
            $"Hi {WebUtility.HtmlEncode(firstName)},",
            $@"<p>We received a request to reset the password for your qrbound account.</p>
               <p style='text-align:center;margin:28px 0;'>
                   <a href='{url}' style='display:inline-block;padding:12px 28px;background:#0B5CFF;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:bold;'>Choose a new password</a>
               </p>
               <p>This link works once and expires in {minutes} minutes.</p>
               <p>If you didn't ask to reset your password, you can ignore this email; your password won't change.</p>
               <p style='font-size:12px;color:#666;word-break:break-all;'>If the button doesn't work, copy this link into your browser:<br>{url}</p>");

        return SendEmailAsync(email, "Reset your qrbound password", body);
    }

    /// <inheritdoc/>
    public Task<bool> SendPasswordChangedEmailAsync(string email, string firstName, string forgotPasswordUrl)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email cannot be null or empty", nameof(email));
        }

        var body = BrandedEmail(
            $"Hi {WebUtility.HtmlEncode(firstName)},",
            $@"<p>The password for your qrbound account was just changed.</p>
               <p>If this was you, there's nothing else to do.</p>
               <p><strong>If it wasn't you,</strong> reset your password right away at
                  <a href='{WebUtility.HtmlEncode(forgotPasswordUrl)}'>{WebUtility.HtmlEncode(forgotPasswordUrl)}</a>
                  and check the passkeys on your account in Settings.</p>");

        return SendEmailAsync(email, "Your qrbound password was changed", body);
    }

    /// <summary>Wraps content in the qrbound email layout (royal-blue header).</summary>
    private static string BrandedEmail(string greeting, string contentHtml) => $@"
        <html>
        <body style='margin:0;padding:0;background:#f3f5fb;font-family:Arial,sans-serif;line-height:1.6;color:#1e2537;'>
            <div style='max-width:560px;margin:0 auto;padding:24px;'>
                <div style='background:#1F43AD;color:#ffffff;padding:20px;text-align:center;border-radius:10px 10px 0 0;font-size:24px;font-weight:bold;'>qrbound</div>
                <div style='background:#ffffff;padding:28px;border-radius:0 0 10px 10px;'>
                    <p>{greeting}</p>
                    {contentHtml}
                </div>
                <p style='text-align:center;font-size:12px;color:#7a8296;'>&copy; {DateTime.UtcNow.Year} qrbound</p>
            </div>
        </body>
        </html>";

    /// <summary>
    /// Sends an email using SMTP
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="body">Email body (HTML)</param>
    /// <returns>True if email was sent successfully, false otherwise</returns>
    private async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
        // Validate settings before attempting to send
        ValidateSettings();

        try
        {
            using var smtpClient = new SmtpClient(_emailSettings.SmtpHost, _emailSettings.SmtpPort)
            {
                Credentials = new NetworkCredential(_emailSettings.SmtpUser, _emailSettings.SmtpPassword),
                EnableSsl = true,
                Timeout = 30000 // 30 seconds
            };

            var mailMessage = new MailMessage
            {
                From = new MailAddress(_emailSettings.FromEmail, _emailSettings.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };

            mailMessage.To.Add(to);

            await smtpClient.SendMailAsync(mailMessage);
            return true;
        }
        catch (SmtpException ex)
        {
            // Log the error but don't throw - return false to indicate failure
            _logger.LogError(ex, "SMTP error sending email to {Email}. Status: {StatusCode}", to, ex.StatusCode);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending email to {Email}", to);
            return false;
        }
    }

    /// <summary>
    /// Validates email settings configuration
    /// </summary>
    private void ValidateSettings()
    {
        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpHost))
        {
            throw new InvalidOperationException("SMTP host is not configured");
        }

        if (_emailSettings.SmtpPort <= 0)
        {
            throw new InvalidOperationException("SMTP port is not configured");
        }

        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpUser))
        {
            throw new InvalidOperationException("SMTP user is not configured");
        }

        if (string.IsNullOrWhiteSpace(_emailSettings.SmtpPassword))
        {
            throw new InvalidOperationException("SMTP password is not configured");
        }

        if (string.IsNullOrWhiteSpace(_emailSettings.FromEmail))
        {
            throw new InvalidOperationException("From email is not configured");
        }
    }
}
