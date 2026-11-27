# Quick SMTP Setup Script for QR Business Exchange
# This script helps you configure email settings using User Secrets (secure)

Write-Host "=== QR Business Exchange - SMTP Configuration ===" -ForegroundColor Cyan
Write-Host ""

# Navigate to API project
$projectPath = "src\QRBusinessExchange.API"
if (-not (Test-Path $projectPath)) {
    Write-Host "Error: Cannot find API project at $projectPath" -ForegroundColor Red
    exit 1
}

Set-Location $projectPath

# Initialize user secrets if not already done
Write-Host "Initializing user secrets..." -ForegroundColor Yellow
dotnet user-secrets init

Write-Host ""
Write-Host "Choose your SMTP provider:" -ForegroundColor Green
Write-Host "1. Gmail (recommended for testing)"
Write-Host "2. Outlook/Office 365"
Write-Host "3. SendGrid (recommended for production)"
Write-Host "4. Mailtrap (development/testing only)"
Write-Host "5. Custom SMTP server"
Write-Host ""

$choice = Read-Host "Enter choice (1-5)"

switch ($choice) {
    "1" {
        # Gmail
        Write-Host ""
        Write-Host "=== Gmail Configuration ===" -ForegroundColor Cyan
        Write-Host "IMPORTANT: You need a Gmail App Password" -ForegroundColor Yellow
        Write-Host "1. Go to: https://myaccount.google.com/apppasswords"
        Write-Host "2. Enable 2-Step Verification if not already"
        Write-Host "3. Create an App Password for 'Mail'"
        Write-Host ""
        
        $email = Read-Host "Enter your Gmail address"
        $password = Read-Host "Enter your App Password (16 characters)" -AsSecureString
        $passwordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
        
        dotnet user-secrets set "EmailSettings:SmtpHost" "smtp.gmail.com"
        dotnet user-secrets set "EmailSettings:SmtpPort" "587"
        dotnet user-secrets set "EmailSettings:SmtpUser" $email
        dotnet user-secrets set "EmailSettings:SmtpPassword" $passwordPlain
        dotnet user-secrets set "EmailSettings:FromEmail" $email
        dotnet user-secrets set "EmailSettings:FromName" "QR Business Exchange"
        
        Write-Host ""
        Write-Host "? Gmail configuration saved!" -ForegroundColor Green
    }
    "2" {
        # Outlook
        Write-Host ""
        Write-Host "=== Outlook/Office 365 Configuration ===" -ForegroundColor Cyan
        
        $email = Read-Host "Enter your Outlook email"
        $password = Read-Host "Enter your password" -AsSecureString
        $passwordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
        
        dotnet user-secrets set "EmailSettings:SmtpHost" "smtp.office365.com"
        dotnet user-secrets set "EmailSettings:SmtpPort" "587"
        dotnet user-secrets set "EmailSettings:SmtpUser" $email
        dotnet user-secrets set "EmailSettings:SmtpPassword" $passwordPlain
        dotnet user-secrets set "EmailSettings:FromEmail" $email
        dotnet user-secrets set "EmailSettings:FromName" "QR Business Exchange"
        
        Write-Host ""
        Write-Host "? Outlook configuration saved!" -ForegroundColor Green
    }
    "3" {
        # SendGrid
        Write-Host ""
        Write-Host "=== SendGrid Configuration ===" -ForegroundColor Cyan
        Write-Host "1. Sign up at: https://sendgrid.com"
        Write-Host "2. Create API Key (Settings ? API Keys)"
        Write-Host "3. Verify sender email"
        Write-Host ""
        
        $apiKey = Read-Host "Enter your SendGrid API Key" -AsSecureString
        $apiKeyPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($apiKey))
        $fromEmail = Read-Host "Enter verified sender email"
        
        dotnet user-secrets set "EmailSettings:SmtpHost" "smtp.sendgrid.net"
        dotnet user-secrets set "EmailSettings:SmtpPort" "587"
        dotnet user-secrets set "EmailSettings:SmtpUser" "apikey"
        dotnet user-secrets set "EmailSettings:SmtpPassword" $apiKeyPlain
        dotnet user-secrets set "EmailSettings:FromEmail" $fromEmail
        dotnet user-secrets set "EmailSettings:FromName" "QR Business Exchange"
        
        Write-Host ""
        Write-Host "? SendGrid configuration saved!" -ForegroundColor Green
    }
    "4" {
        # Mailtrap
        Write-Host ""
        Write-Host "=== Mailtrap Configuration ===" -ForegroundColor Cyan
        Write-Host "1. Sign up at: https://mailtrap.io"
        Write-Host "2. Get credentials from your inbox"
        Write-Host ""
        
        $username = Read-Host "Enter Mailtrap username"
        $password = Read-Host "Enter Mailtrap password" -AsSecureString
        $passwordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
        
        dotnet user-secrets set "EmailSettings:SmtpHost" "smtp.mailtrap.io"
        dotnet user-secrets set "EmailSettings:SmtpPort" "2525"
        dotnet user-secrets set "EmailSettings:SmtpUser" $username
        dotnet user-secrets set "EmailSettings:SmtpPassword" $passwordPlain
        dotnet user-secrets set "EmailSettings:FromEmail" "test@qrbusinessexchange.com"
        dotnet user-secrets set "EmailSettings:FromName" "QR Business Exchange (Dev)"
        
        Write-Host ""
        Write-Host "? Mailtrap configuration saved!" -ForegroundColor Green
    }
    "5" {
        # Custom
        Write-Host ""
        Write-Host "=== Custom SMTP Configuration ===" -ForegroundColor Cyan
        
        $host = Read-Host "Enter SMTP host (e.g., smtp.example.com)"
        $port = Read-Host "Enter SMTP port (587 for TLS, 465 for SSL)"
        $username = Read-Host "Enter SMTP username"
        $password = Read-Host "Enter SMTP password" -AsSecureString
        $passwordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto([Runtime.InteropServices.Marshal]::SecureStringToBSTR($password))
        $fromEmail = Read-Host "Enter sender email"
        $fromName = Read-Host "Enter sender name (e.g., QR Business Exchange)"
        
        dotnet user-secrets set "EmailSettings:SmtpHost" $host
        dotnet user-secrets set "EmailSettings:SmtpPort" $port
        dotnet user-secrets set "EmailSettings:SmtpUser" $username
        dotnet user-secrets set "EmailSettings:SmtpPassword" $passwordPlain
        dotnet user-secrets set "EmailSettings:FromEmail" $fromEmail
        dotnet user-secrets set "EmailSettings:FromName" $fromName
        
        Write-Host ""
        Write-Host "? Custom SMTP configuration saved!" -ForegroundColor Green
    }
    default {
        Write-Host "Invalid choice" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "=== Configuration Complete ===" -ForegroundColor Green
Write-Host ""
Write-Host "Your SMTP credentials are securely stored in User Secrets."
Write-Host "They will NOT be committed to source control."
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Run the API: dotnet run"
Write-Host "2. Test registration: POST /api/Auth/register"
Write-Host "3. Check email inbox for verification email"
Write-Host ""
Write-Host "To view your secrets: dotnet user-secrets list"
Write-Host "To remove a secret: dotnet user-secrets remove 'EmailSettings:SmtpPassword'"
Write-Host ""

Set-Location ../..
