# Email Functionality Testing Guide

## Overview

This guide provides multiple methods to test the email functionality in QR Business Exchange.

---

## Prerequisites

? SMTP configuration completed (see `docs/SMTP-Configuration-Guide.md`)
? API project builds successfully
? Email credentials configured in User Secrets or appsettings

---

## Method 1: Test Endpoint (Recommended - Fastest)

### Step 1: Start the API

```bash
cd src/QRBusinessExchange.API
dotnet run
```

### Step 2: Check SMTP Configuration Status

**Using Swagger UI**:
1. Open: `https://localhost:<port>/swagger`
2. Find: `GET /api/EmailTest/smtp-status`
3. Click "Try it out" ? "Execute"

**Expected Response (Configured)**:
```json
{
  "configured": true,
  "message": "SMTP configuration is valid",
  "serviceType": "QRBusinessExchange.Infrastructure.Services.EmailService",
  "note": "Configuration validated..."
}
```

**Expected Response (Not Configured)**:
```json
{
  "configured": false,
  "error": "SMTP host is not configured",
  "hint": "Configure EmailSettings in appsettings.json or User Secrets"
}
```

**Using cURL**:
```bash
curl https://localhost:5001/api/EmailTest/smtp-status -k
```

---

### Step 3: Send Test Email

**Using Swagger UI**:
1. Find: `POST /api/EmailTest/send-test-email`
2. Click "Try it out"
3. Enter your email in the `email` parameter
4. Click "Execute"

**Using cURL**:
```bash
curl -X POST "https://localhost:5001/api/EmailTest/send-test-email?email=your-email@example.com" -k
```

**Using PowerShell**:
```powershell
$email = "your-email@example.com"
Invoke-RestMethod -Uri "https://localhost:5001/api/EmailTest/send-test-email?email=$email" -Method Post -SkipCertificateCheck
```

**Expected Response (Success)**:
```json
{
  "success": true,
  "message": "Test email sent successfully to your-email@example.com",
  "timestamp": "2025-01-29T12:34:56.789Z",
  "note": "Check your inbox (or spam folder) for the verification email"
}
```

**Expected Response (SMTP Error)**:
```json
{
  "success": false,
  "error": "SMTP is not properly configured",
  "details": "SMTP host is not configured",
  "hint": "Check appsettings.json or User Secrets..."
}
```

---

### Step 4: Check Email

- **Gmail/Outlook**: Check inbox and spam folder
- **Mailtrap**: Check Mailtrap inbox at https://mailtrap.io
- **SendGrid**: Check SendGrid activity feed

**Email Should Contain**:
- Subject: "Verify Your Email - QR Business Exchange"
- Verification link
- QR Business Exchange branding
- Professional HTML formatting

---

## Method 2: Integration Tests

### Run All Email Tests

```bash
cd tests/QRBusinessExchange.IntegrationTests
dotnet test --filter "FullyQualifiedName~EmailServiceTests"
```

### Run Specific Tests

**Check SMTP Status**:
```bash
dotnet test --filter "FullyQualifiedName~SmtpStatus_ReturnsConfigurationStatus"
```

**Test Validation**:
```bash
dotnet test --filter "FullyQualifiedName~SendTestEmail_WithInvalidEmail_ReturnsBadRequest"
```

**Send Real Test Email** (requires configuration):
```bash
# First, remove the Skip attribute from the test
# Then run:
dotnet test --filter "FullyQualifiedName~SendTestEmail_WithValidEmail_SendsEmail"
```

---

## Method 3: Test via User Registration

This tests the email service in the actual application flow.

### Step 1: Register New User

**Using Swagger UI**:
1. Open: `https://localhost:<port>/swagger`
2. Find: `POST /api/Auth/register`
3. Click "Try it out"
4. Enter details:

```json
{
  "email": "test@example.com",
  "password": "Test@123456",
  "firstName": "Test",
  "lastName": "User",
  "companyName": "Test Company"
}
```

5. Click "Execute"

**Using cURL**:
```bash
curl -X POST "https://localhost:5001/api/Auth/register" \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Test@123456",
    "firstName": "Test",
    "lastName": "User"
  }' \
  -k
```

### Step 2: Check Email

The registration process sends a verification email asynchronously. Check:
- Your email inbox
- Spam folder
- Mailtrap inbox (if using Mailtrap)

### Step 3: Verify Email Content

The email should have:
- ? Subject: "Verify Your Email - QR Business Exchange"
- ? Personalized greeting: "Hello Test User"
- ? Verification link
- ? Professional HTML formatting
- ? Footer with copyright

---

## Method 4: Blazor Admin Panel (Future Enhancement)

For ongoing email testing, you could create a Blazor page:

**Location**: `src/QRBusinessExchange.Web/Components/Pages/Admin/EmailTest.razor`

**Features**:
- SMTP status indicator
- Send test email form
- Email history/logs
- Real-time delivery status

*Note: Not yet implemented - future enhancement*

---

## Troubleshooting

### Issue: "SMTP host is not configured"

**Solution**:
```bash
# Check configuration
cd src/QRBusinessExchange.API
dotnet user-secrets list

# Should show EmailSettings values
# If empty, run: .\scripts\Setup-SMTP.ps1
```

---

### Issue: Email Sent but Not Received

**Checklist**:
- ? Check spam/junk folder
- ? Verify email address is correct
- ? Check SMTP provider logs (SendGrid/Mailtrap dashboard)
- ? Verify sender email is verified (for production services)
- ? Check API logs for errors:

```bash
# View logs
dotnet run --project src/QRBusinessExchange.API
# Watch for email service logs
```

---

### Issue: Authentication Failed

**Gmail**:
- ? Using App Password (not account password)?
- ? 2-Factor Authentication enabled?
- ? App Password has no spaces?

**Outlook**:
- ? SMTP enabled in account settings?
- ? Correct password?

**SendGrid**:
- ? API key starts with "SG."?
- ? Username is literally "apikey"?
- ? API key has "Mail Send" permissions?

---

### Issue: Connection Timeout

**Solutions**:
- ? Check firewall allows outbound SMTP (port 587/465)
- ? Try alternate port (587 if using 465, vice versa)
- ? Verify SMTP host is correct
- ? Check network/VPN isn't blocking SMTP

---

### Issue: SSL/TLS Error

**Gmail**:
- Use port 587 with TLS
- OR port 465 with SSL

**Code Fix** (if needed):
In `EmailService.cs`, verify:
```csharp
smtp.EnableSsl = true;  // Should be true for ports 587 and 465
```

---

## Testing Checklist

Before deploying to production:

### SMTP Configuration
- [ ] Credentials configured (User Secrets for dev)
- [ ] SMTP status returns "configured: true"
- [ ] Test email sends successfully
- [ ] Email arrives in inbox (not spam)

### Email Content
- [ ] Subject line correct
- [ ] HTML formatting displays properly
- [ ] Links are clickable
- [ ] Images load (if any)
- [ ] Footer includes unsubscribe (for production)

### Functionality
- [ ] User registration sends verification email
- [ ] Verification link works
- [ ] Email is sent asynchronously (doesn't block registration)
- [ ] Errors are logged but don't crash registration

### Security
- [ ] Passwords not in source control
- [ ] Using TLS/SSL encryption
- [ ] Sender email verified (production)
- [ ] Rate limiting in place (production)

---

## Quick Test Commands

### Full Test Suite
```bash
# 1. Check SMTP status
curl https://localhost:5001/api/EmailTest/smtp-status -k

# 2. Send test email
curl -X POST "https://localhost:5001/api/EmailTest/send-test-email?email=test@example.com" -k

# 3. Register user (tests real flow)
curl -X POST "https://localhost:5001/api/Auth/register" \
  -H "Content-Type: application/json" \
  -d '{"email":"test@example.com","password":"Test@123456","firstName":"Test","lastName":"User"}' \
  -k

# 4. Run integration tests
dotnet test --filter "FullyQualifiedName~EmailServiceTests"
```

---

## Provider-Specific Testing

### Mailtrap (Development)

**Advantages**:
- ? Emails never actually send (safe)
- ? Instant delivery to test inbox
- ? HTML/text preview
- ? Check spam score
- ? Inspect headers

**Testing**:
1. Send test email via API
2. Go to https://mailtrap.io
3. Open inbox
4. Verify email appearance

---

### Gmail (Testing)

**Testing**:
1. Send to your Gmail
2. Check "All Mail" (not just inbox)
3. Check "Spam"
4. Verify sender shows correctly

---

### SendGrid (Production)

**Testing**:
1. Send test email
2. Check SendGrid Dashboard ? Activity Feed
3. Verify delivery status
4. Check bounce/spam reports

**Monitor**:
- Delivery rate
- Open rate (requires tracking)
- Bounce rate
- Spam reports

---

## Performance Testing

### Test Email Volume

```bash
# Send multiple emails (be careful with rate limits!)
for i in {1..10}; do
  curl -X POST "https://localhost:5001/api/EmailTest/send-test-email?email=test$i@example.com" -k
  sleep 1
done
```

### Monitor Performance

Check logs for:
- Send duration
- Success rate
- Error patterns

---

## Next Steps After Testing

Once email is working:

1. **Remove Test Endpoint** (or protect it):
```csharp
#if DEBUG
[ApiController]
[Route("api/[controller]")]
public class EmailTestController : ControllerBase
#endif
```

2. **Add Email Logging** for production monitoring

3. **Implement Email Templates** for better branding

4. **Add Email Queue** for high volume (Hangfire/background service)

5. **Set Up Email Analytics** (open rates, click tracking)

---

## Email Testing Best Practices

### Development
- ? Use Mailtrap (emails don't send)
- ? Use User Secrets for credentials
- ? Test all email types
- ? Verify HTML rendering

### Staging
- ? Use real provider (SendGrid/SES)
- ? Send to test emails only
- ? Monitor delivery rates
- ? Test spam filters

### Production
- ? Verified sender domain
- ? SPF/DKIM configured
- ? Monitor bounce rates
- ? Unsubscribe functionality
- ? Comply with email regulations

---

## Summary

You now have multiple ways to test email:

1. **? Test Endpoint**: `/api/EmailTest/send-test-email` (fastest)
2. **? Integration Tests**: `EmailServiceTests.cs` (automated)
3. **? User Registration**: Real application flow
4. **? SMTP Status**: `/api/EmailTest/smtp-status` (health check)

**Start with**: Test endpoint ? Send to your email ? Verify receipt

**Questions?** Check `docs/SMTP-Configuration-Guide.md` for SMTP setup help.

---

**Ready to test! ??**
