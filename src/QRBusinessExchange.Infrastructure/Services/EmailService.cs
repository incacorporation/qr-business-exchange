using System.Net;
using System.Net.Mail;
using Microsoft.EntityFrameworkCore;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailService"/> class
    /// </summary>
    /// <param name="emailSettings">Email configuration settings</param>
    /// <param name="context">The application database context</param>
    /// <param name="baseUrl">The base URL for verification links (e.g., "https://qrbusinessexchange.com")</param>
    public EmailService(IOptions<EmailSettings> emailSettings, ApplicationDbContext context, string baseUrl = "https://qrbusinessexchange.com")
    {
        _emailSettings = emailSettings?.Value ?? throw new ArgumentNullException(nameof(emailSettings));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _baseUrl = baseUrl ?? throw new ArgumentNullException(nameof(baseUrl));

        ValidateSettings();
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

            var verificationUrl = $"{_baseUrl}/verify-email?token={verificationToken}&userId={userId}";

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

    /// <summary>
    /// Sends an email using SMTP
    /// </summary>
    /// <param name="to">Recipient email address</param>
    /// <param name="subject">Email subject</param>
    /// <param name="body">Email body (HTML)</param>
    /// <returns>True if email was sent successfully, false otherwise</returns>
    private async Task<bool> SendEmailAsync(string to, string subject, string body)
    {
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
            Console.Error.WriteLine($"SMTP error sending email to {to}: {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error sending email to {to}: {ex.Message}");
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
