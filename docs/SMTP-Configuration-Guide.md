# SMTP Configuration Guide

## Overview

This guide helps you configure email settings for the QR Business Exchange API to enable user email verification, connection notifications, and other email features.

---

## Configuration File Locations

- **Production**: `appsettings.json`
- **Development**: `appsettings.Development.json`
- **User Secrets**: Right-click project ? Manage User Secrets (recommended for development)

---

## Required Settings

```json
"EmailSettings": {
  "SmtpHost": "smtp.example.com",     // SMTP server address
  "SmtpPort": 587,                     // SMTP port (587 for TLS, 465 for SSL)
  "SmtpUser": "your-email@example.com", // SMTP username/email
  "SmtpPassword": "your-password",     // SMTP password (use secrets!)
  "FromEmail": "noreply@example.com",  // Sender email address
  "FromName": "QR Business Exchange"   // Sender display name
}
```

---

## Provider-Specific Configurations

### 1. Gmail (Recommended for Testing)

**Requirements**:
- Enable 2-Factor Authentication on your Google account
- Create an App Password (not your regular password)

**Steps**:
1. Go to: https://myaccount.google.com/security
2. Enable 2-Step Verification
3. Go to: https://myaccount.google.com/apppasswords
4. Create app password for "Mail"
5. Use generated password in configuration

**Configuration**:
```json
"EmailSettings": {
  "SmtpHost": "smtp.gmail.com",
  "SmtpPort": 587,
  "SmtpUser": "your-email@gmail.com",
  "SmtpPassword": "xxxx xxxx xxxx xxxx",  // 16-character app password
  "FromEmail": "your-email@gmail.com",
  "FromName": "QR Business Exchange"
}
```

**Notes**:
- ? Free for low-volume use
- ? Reliable delivery
- ?? Daily send limit: ~500 emails
- ?? Use app password, NOT account password

---

### 2. Outlook / Office 365

**Configuration**:
```json
"EmailSettings": {
  "SmtpHost": "smtp.office365.com",
  "SmtpPort": 587,
  "SmtpUser": "your-email@outlook.com",
  "SmtpPassword": "your-password",
  "FromEmail": "your-email@outlook.com",
  "FromName": "QR Business Exchange"
}
```

**Notes**:
- ? Good for business accounts
- ? Office 365 integration
- ?? Daily send limit: ~300 emails (varies by plan)

---

### 3. SendGrid (Recommended for Production)

**Setup**:
1. Sign up at: https://sendgrid.com
2. Create API Key (Settings ? API Keys)
3. Verify sender identity

**Configuration**:
```json
"EmailSettings": {
  "SmtpHost": "smtp.sendgrid.net",
  "SmtpPort": 587,
  "SmtpUser": "apikey",  // Literally "apikey"
  "SmtpPassword": "SG.xxxxxxxxxxxxxxxxxxxxx",  // Your SendGrid API key
  "FromEmail": "noreply@qrbusinessexchange.com",  // Must be verified
  "FromName": "QR Business Exchange"
}
```

**Notes**:
- ? Free tier: 100 emails/day
- ? Professional email delivery
- ? Great analytics and tracking
- ? Scalable for production
- ?? Requires domain verification for production

---

### 4. Mailtrap (Perfect for Development/Testing)

**Setup**:
1. Sign up at: https://mailtrap.io
2. Get credentials from inbox settings

**Configuration**:
```json
"EmailSettings": {
  "SmtpHost": "smtp.mailtrap.io",
  "SmtpPort": 2525,
  "SmtpUser": "your-mailtrap-username",
  "SmtpPassword": "your-mailtrap-password",
  "FromEmail": "test@qrbusinessexchange.com",
  "FromName": "QR Business Exchange (Dev)"
}
```

**Notes**:
- ? FREE for development
- ? Emails don't actually send (caught in inbox)
- ? Perfect for testing email templates
- ? No risk of accidentally emailing users
- ?? Development only - emails never reach recipients

---

### 5. AWS SES (Amazon Simple Email Service)

**Configuration**:
```json
"EmailSettings": {
  "SmtpHost": "email-smtp.us-east-1.amazonaws.com",  // Your region
  "SmtpPort": 587,
  "SmtpUser": "YOUR_SMTP_USERNAME",  // From SES console
  "SmtpPassword": "YOUR_SMTP_PASSWORD",  // From SES console
  "FromEmail": "noreply@qrbusinessexchange.com",  // Verified email
  "FromName": "QR Business Exchange"
}
```

**Notes**:
- ? Very cheap (first 62,000 emails free with EC2)
- ? Highly scalable
- ?? Requires AWS account
- ?? More complex setup

---

## Security Best Practices

### Use User Secrets (Development)

**Never commit passwords to source control!**

**Setup User Secrets**:
```bash
cd src/QRBusinessExchange.API
dotnet user-secrets init
dotnet user-secrets set "EmailSettings:SmtpPassword" "your-password"
```

**User secrets override appsettings.json** values automatically.

---

### Use Environment Variables (Production)

For deployment, use environment variables:

```bash
# Linux/Mac
export EmailSettings__SmtpPassword="your-password"

# Windows
set EmailSettings__SmtpPassword=your-password

# Docker
docker run -e EmailSettings__SmtpPassword="your-password" ...
```

Note: Use double underscore `__` for nested configuration.

---

### Use Azure Key Vault (Production - Recommended)

For production Azure deployments:

1. Store secrets in Azure Key Vault
2. Configure Key Vault access in `Program.cs`:

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri("https://your-keyvault.vault.azure.net/"),
    new DefaultAzureCredential());
```

---

## Quick Setup Guide

### Option 1: Gmail (5 minutes)

1. **Create Gmail App Password**:
   - Go to: https://myaccount.google.com/apppasswords
   - Generate password

2. **Configure User Secrets**:
```bash
cd src/QRBusinessExchange.API
dotnet user-secrets set "EmailSettings:SmtpHost" "smtp.gmail.com"
dotnet user-secrets set "EmailSettings:SmtpPort" "587"
dotnet user-secrets set "EmailSettings:SmtpUser" "your-email@gmail.com"
dotnet user-secrets set "EmailSettings:SmtpPassword" "xxxx xxxx xxxx xxxx"
dotnet user-secrets set "EmailSettings:FromEmail" "your-email@gmail.com"
dotnet user-secrets set "EmailSettings:FromName" "QR Business Exchange"
```

3. **Test**:
```bash
dotnet run
# Register a user and check email
```

---

### Option 2: Mailtrap (3 minutes - Testing Only)

1. **Sign up**: https://mailtrap.io
2. **Get credentials** from inbox
3. **Update** `appsettings.Development.json`:
```json
"EmailSettings": {
  "SmtpHost": "smtp.mailtrap.io",
  "SmtpPort": 2525,
  "SmtpUser": "your-username",
  "SmtpPassword": "your-password",
  "FromEmail": "test@qrbusinessexchange.com",
  "FromName": "QR Business Exchange (Dev)"
}
```

4. **Run and test** - emails appear in Mailtrap inbox

---

## Testing Email Configuration

### Test Registration Flow

1. Start API: `dotnet run`
2. Use Swagger: `https://localhost:5001/swagger`
3. Call `POST /api/Auth/register`:
```json
{
  "email": "test@example.com",
  "password": "Test@123456",
  "firstName": "Test",
  "lastName": "User"
}
```
4. Check email inbox (or Mailtrap for dev)

### Verify Configuration

Add to `Program.cs` (temporary):
```csharp
var emailSettings = builder.Configuration.GetSection("EmailSettings").Get<EmailSettings>();
Console.WriteLine($"SMTP Host: {emailSettings?.SmtpHost}");
Console.WriteLine($"SMTP Port: {emailSettings?.SmtpPort}");
Console.WriteLine($"From Email: {emailSettings?.FromEmail}");
```

---

## Common Issues & Solutions

### Issue: "SMTP host is not configured"

**Solution**: Check property names in appsettings.json match:
```json
"EmailSettings": {
  "SmtpHost": "",      // NOT "SmtpServer"
  "SmtpUser": "",      // NOT "Username"
  "SmtpPassword": "",  // NOT "Password"
  "FromEmail": ""      // NOT "SenderEmail"
}
```

---

### Issue: "Authentication failed"

**Solutions**:
- **Gmail**: Use App Password, not account password
- **Outlook**: Enable SMTP in account settings
- **All**: Check username is correct (usually email address)

---

### Issue: "Connection refused" or "Timeout"

**Solutions**:
- Check port number (587 for TLS, 465 for SSL)
- Check firewall allows outbound SMTP connections
- Try different port (Gmail: 587 or 465)

---

### Issue: Emails go to spam

**Solutions**:
- Use verified domain (SendGrid/SES)
- Add SPF/DKIM records to DNS
- Use professional email service (not Gmail for production)
- Include unsubscribe link
- Avoid spam trigger words

---

## Production Checklist

Before deploying to production:

- [ ] Use professional email service (SendGrid, AWS SES, etc.)
- [ ] Verify sender domain
- [ ] Configure SPF/DKIM/DMARC records
- [ ] Store passwords in secrets management
- [ ] Test email delivery to major providers (Gmail, Outlook, etc.)
- [ ] Set up email monitoring/analytics
- [ ] Implement retry logic for failed sends
- [ ] Add rate limiting to prevent abuse
- [ ] Include unsubscribe functionality
- [ ] Comply with email regulations (CAN-SPAM, GDPR)

---

## Recommended Configuration by Environment

| Environment | Provider | Why |
|-------------|----------|-----|
| **Development** | Mailtrap | Free, safe, no real emails sent |
| **Staging** | SendGrid Free | Real email testing, 100/day free |
| **Production** | SendGrid/AWS SES | Scalable, professional, reliable |

---

## Quick Reference

### Common SMTP Ports
- **587**: TLS (STARTTLS) - **Recommended**
- **465**: SSL/TLS (Legacy)
- **25**: Plain SMTP (Usually blocked)
- **2525**: Alternative (Mailtrap, etc.)

### Property Mappings
| appsettings.json | EmailSettings Class |
|------------------|-------------------|
| `SmtpHost` | `SmtpHost` |
| `SmtpPort` | `SmtpPort` |
| `SmtpUser` | `SmtpUser` |
| `SmtpPassword` | `SmtpPassword` |
| `FromEmail` | `FromEmail` |
| `FromName` | `FromName` |

---

## Support

For issues:
1. Check application logs
2. Test SMTP credentials with email client
3. Verify firewall/network settings
4. Check provider-specific documentation

---

**Ready to send emails!** ??
