# QR Business Exchange - IONOS Deployment Guide

## Prerequisites
- IONOS VPS or Dedicated Server
- Ubuntu 22.04 LTS (recommended)
- Root or sudo access
- Domain name configured in IONOS

## Step 1: Server Setup

### Connect to Your IONOS Server
```bash
ssh root@your-server-ip
```

### Update System
```bash
apt update && apt upgrade -y
```

### Install .NET 8.0 Runtime
```bash
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
apt update
apt install -y aspnetcore-runtime-8.0
```

### Install Nginx
```bash
apt install -y nginx
```

### Install SQL Server (Optional - if not using external database)
```bash
wget -qO- https://packages.microsoft.com/keys/microsoft.asc | sudo apt-key add -
add-apt-repository "$(wget -qO- https://packages.microsoft.com/config/ubuntu/22.04/mssql-server-2022.list)"
apt update
apt install -y mssql-server
/opt/mssql/bin/mssql-conf setup
```

## Step 2: Create Application User
```bash
useradd -m -s /bin/bash qrbusiness
```

## Step 3: Deploy Application

### Create Application Directory
```bash
mkdir -p /var/www/qrbusiness/api
mkdir -p /var/www/qrbusiness/web
chown -R qrbusiness:qrbusiness /var/www/qrbusiness
```

### Build and Publish Locally (on your machine)
```bash
cd qr-business-exchange
dotnet publish src/QRBusinessExchange.API -c Release -o ./publish/api
dotnet publish src/QRBusinessExchange.Web -c Release -o ./publish/web
```

### Copy Files to Server
```bash
scp -r ./publish/api/* root@your-server-ip:/var/www/qrbusiness/api/
scp -r ./publish/web/* root@your-server-ip:/var/www/qrbusiness/web/
```

### Set Permissions
```bash
chown -R qrbusiness:qrbusiness /var/www/qrbusiness
chmod +x /var/www/qrbusiness/api/QRBusinessExchange.API
chmod +x /var/www/qrbusiness/web/QRBusinessExchange.Web
```

## Step 4: Configure Systemd Services

### API Service
Create `/etc/systemd/system/qrbusiness-api.service`:
```ini
[Unit]
Description=QR Business Exchange API
After=network.target

[Service]
WorkingDirectory=/var/www/qrbusiness/api
ExecStart=/usr/bin/dotnet /var/www/qrbusiness/api/QRBusinessExchange.API.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=qrbusiness-api
User=qrbusiness
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5000

[Install]
WantedBy=multi-user.target
```

### Web Service
Create `/etc/systemd/system/qrbusiness-web.service`:
```ini
[Unit]
Description=QR Business Exchange Web
After=network.target

[Service]
WorkingDirectory=/var/www/qrbusiness/web
ExecStart=/usr/bin/dotnet /var/www/qrbusiness/web/QRBusinessExchange.Web.dll
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=qrbusiness-web
User=qrbusiness
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=ASPNETCORE_URLS=http://localhost:5100

[Install]
WantedBy=multi-user.target
```

### Enable and Start Services
```bash
systemctl daemon-reload
systemctl enable qrbusiness-api
systemctl enable qrbusiness-web
systemctl start qrbusiness-api
systemctl start qrbusiness-web
```

### Check Status
```bash
systemctl status qrbusiness-api
systemctl status qrbusiness-web
```

## Step 5: Configure Nginx

### API Configuration
Create `/etc/nginx/sites-available/qrbusiness-api`:
```nginx
server {
    listen 80;
    server_name api.qrbound.com;
    
    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Web Configuration
Create `/etc/nginx/sites-available/qrbusiness-web`:
```nginx
server {
    listen 80;
    server_name www.qrbound.com qrbound.com;
    
    location / {
        proxy_pass http://localhost:5100;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

### Enable Sites
```bash
ln -s /etc/nginx/sites-available/qrbusiness-api /etc/nginx/sites-enabled/
ln -s /etc/nginx/sites-available/qrbusiness-web /etc/nginx/sites-enabled/
nginx -t
systemctl restart nginx
```

## Step 6: SSL Configuration with Let's Encrypt

### Install Certbot
```bash
apt install -y certbot python3-certbot-nginx
```

### Obtain Certificates
```bash
certbot --nginx -d api.qrbound.com
certbot --nginx -d www.qrbound.com -d qrbound.com
```

### Auto-Renewal
Certbot automatically sets up renewal. Verify:
```bash
certbot renew --dry-run
```

## Step 7: Configure Firewall

```bash
ufw allow 'Nginx Full'
ufw allow OpenSSH
ufw enable
```

## Step 8: Database Configuration

If using the provided connection string, ensure your IONOS server can reach `incacorporation.net`.

For local SQL Server:
```bash
# Configure SQL Server firewall
ufw allow 1433/tcp

# Update appsettings.json on server
nano /var/www/qrbusiness/api/appsettings.json
```

## Step 9: Environment Variables

Create `/var/www/qrbusiness/api/appsettings.Production.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:incacorporation.net,1433;Database=www.qrbound.com;User Id=ssoAdministrator;Encrypt=Mandatory;TrustServerCertificate=False;Connect Timeout=30;Persist Security Info=False"
  },
  "JwtSettings": {
    "Issuer": "QRBusinessExchange",
    "Audience": "QRBusinessExchangeAPI"
  }
}
```

Secrets are not in this file. Put them in the service's environment, readable only by root
(`sudo systemctl edit qrbusiness-api`):
```ini
[Service]
Environment=Database__Password=<password>
Environment=JwtSettings__SecretKey=<output of: openssl rand -base64 64 | tr -d '\n'>
# Only if you need POST /api/auth/register-admin; leave it out to keep that endpoint switched off.
# Environment=Admin__RegistrationSecret=<output of: openssl rand -base64 32>
```

The API refuses to start if `JwtSettings__SecretKey` is missing, shorter than 32 bytes or a sample value.
Use a different key in production than in development, and changing it signs everyone out.

Password-reset links are signed with ASP.NET Core Data Protection keys. Persist them so links survive restarts,
for example by mounting a volume and calling `PersistKeysToFileSystem` in `Program.cs`.

SQL Server (Docker container `sqlDB`, port 1433) uses a Let's Encrypt certificate for `incacorporation.net`
and forces encryption, so the API verifies the server's identity (`TrustServerCertificate=False`).
The certificate is renewed by certbot on the host; its deploy hook
(`/etc/letsencrypt/renewal-hooks/deploy/mssql.sh`) must copy the new files into the container and restart it,
otherwise connections fail once the old certificate expires.

## Step 10: Monitoring & Logs

### View Logs
```bash
journalctl -u qrbusiness-api -f
journalctl -u qrbusiness-web -f
```

### Setup Log Rotation
Create `/etc/logrotate.d/qrbusiness`:
```
/var/log/qrbusiness/*.log {
    daily
    rotate 14
    compress
    delaycompress
    notifempty
    missingok
}
```

## Maintenance

### Update Application
```bash
# Stop services
systemctl stop qrbusiness-api qrbusiness-web

# Deploy new version
# ... upload new files ...

# Restart services
systemctl start qrbusiness-api qrbusiness-web
```

### Backup Database
```bash
# Create backup script
nano /usr/local/bin/backup-qrbusiness.sh
```

```bash
#!/bin/bash
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_DIR="/backups/qrbusiness"
mkdir -p $BACKUP_DIR
sqlcmd -S localhost -U sa -P 'YourPassword' -Q "BACKUP DATABASE [www.qrbound.com] TO DISK='$BACKUP_DIR/qrbusiness_$DATE.bak'"
```

```bash
chmod +x /usr/local/bin/backup-qrbusiness.sh

# Add to cron
crontab -e
# Add line: 0 2 * * * /usr/local/bin/backup-qrbusiness.sh
```

## Troubleshooting

### Service Won't Start
```bash
systemctl status qrbusiness-api
journalctl -xe
```

### Database Connection Issues
```bash
# Test connection
telnet incacorporation.net 1433
```

### Nginx Issues
```bash
nginx -t
tail -f /var/log/nginx/error.log
```

## IONOS-Specific Notes

1. **DNS Configuration**: Use IONOS DNS manager to point domains to your server IP
2. **Backup**: Use IONOS backup solutions for server snapshots
3. **Monitoring**: Consider IONOS monitoring tools
4. **Email**: Configure SMTP settings to use IONOS mail servers

## Security Recommendations

1. Keep system updated: `apt update && apt upgrade`
2. Configure fail2ban: `apt install fail2ban`
3. Regular backups
4. Monitor logs for suspicious activity
5. Use strong passwords
6. Keep SSL certificates updated
7. Regular security audits

## Support

For IONOS-specific issues:
- IONOS Support: https://www.ionos.com/help
- Phone: Check your IONOS account for support numbers

For application issues:
- GitHub: https://github.com/incacorporation/qr-business-exchange/issues
