# QRBusinessExchange Publish Configuration Guide

## Updated Publish Profiles

### Certificate Configuration
Both publish profiles now include SSL/TLS certificate settings to handle untrusted certificates:

```xml
<AllowUntrustedCertificate>true</AllowUntrustedCertificate>
<EncryptionMethod>basic</EncryptionMethod>
<TrustServerCertificate>true</TrustServerCertificate>
```

### Projects Configured for Deployment

#### 1. QRBusinessExchange.API
- **Location**: `src/QRBusinessExchange.API/Properties/PublishProfiles/IISProfile.pubxml`
- **Target**: `https://secured.incacorporation.com`
- **IIS App Path**: `webapi.qrbound.com`
- **Authentication**: Administrator (Basic)

#### 2. QRBusinessExchange.Web (Blazor SSR)
- **Location**: `src/QRBusinessExchange.Web/Properties/PublishProfiles/IISProfile.pubxml`
- **Target**: `https://secured.incacorporation.com`
- **IIS App Path**: `Default Web Site`
- **Authentication**: Administrator (Basic)

## Publishing from Visual Studio

### Method 1: Using Visual Studio UI (Recommended)

1. **Select Project**: Right-click on `QRBusinessExchange.API` or `QRBusinessExchange.Web`
2. **Publish**: Click **Publish**
3. **Select Profile**: Choose **IISProfile**
4. **Review Settings**: The certificate validation is now configured to allow untrusted certificates
5. **Enter Credentials**: If prompted, enter your Administrator username and password
6. **Publish**: Click **Publish** to deploy

### Method 2: Using Command Line (PowerShell)

```powershell
cd "H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange"

# Publish API
dotnet publish src/QRBusinessExchange.API/QRBusinessExchange.API.csproj `
  -c Release `
  -p:PublishProfile=IISProfile

# Publish Web
dotnet publish src/QRBusinessExchange.Web/QRBusinessExchange.Web.csproj `
  -c Release `
  -p:PublishProfile=IISProfile
```

### Method 3: Using MSDeploy Directly

```powershell
$msdeploy = "C:\Program Files (x86)\IIS\Microsoft Web Deploy\msdeploy.exe"

& $msdeploy -verb:sync `
  -source:contentPath=".\src\QRBusinessExchange.API\bin\Release\net10.0\publish" `
  -dest:contentPath="webapi.qrbound.com",computerName="https://secured.incacorporation.com:443/msdeploy.axd?site=webapi.qrbound.com",userName="Administrator",password="YourPassword",authtype="Basic" `
  -allowUntrusted
```

## Configuration Details

### Web Deploy Settings
- **WebPublishMethod**: MSDeploy
- **MSDeployPublishMethod**: WMSVC (Web Management Service)
- **Self-Contained**: Yes (net10.0 standalone runtime)
- **Runtime Identifier**: win-x64
- **Encryption**: Basic Authentication over HTTPS
- **Backup**: Enabled (Previous versions maintained)
- **Application Offline**: Enabled during deployment

### Troubleshooting

#### Still Getting Certificate Error?

If you continue to see certificate validation errors:

1. **Option A**: Verify the server certificate manually
```powershell
# Test the connection
Test-NetConnection -ComputerName secured.incacorporation.com -Port 443
```

2. **Option B**: Add additional settings to `.pubxml`
```xml
<SkipExtraFilesOnServer>true</SkipExtraFilesOnServer>
<MSDeployUseChecksum>true</MSDeployUseChecksum>
<MSDeployAddEditParams>
  <MSDeployParameter Include="IIS Web Application Name" Value="webapi.qrbound.com" />
</MSDeployUseChecksum>
```

3. **Option C**: Use a batch file with verbose logging
```batch
set MSDEPLOY_TRACE=true
msdeploy.exe -verb:sync -source:contentPath=".\bin\Release\net10.0\publish" ...
```

#### Authentication Failures

- Verify Administrator credentials on the target server
- Ensure the account has permissions for Web Management Service (WMSVC) operations
- Check if the account is part of the appropriate IIS groups

#### Deployment Hangs

- Check network connectivity to `secured.incacorporation.com:443`
- Verify Web Management Service (WMSVC) is running on the target server
- Try with `EnableMsDeployAppOffline=false` to skip app pool restart

## Security Notes

⚠️ **Important**: 
- `AllowUntrustedCertificate=true` bypasses SSL/TLS validation — use only for:
  - Development/Testing environments
  - Self-signed certificates
  - Pre-production scenarios

For **Production**, obtain a proper SSL certificate and reload the `.pubxml` to remove the override.

## Next Steps

1. Update `Administrator` password in Visual Studio if needed (Profile Properties → Edit)
2. Adjust `DeployIisAppPath` to match your actual IIS site name
3. Test the first deployment to verify connectivity
4. Monitor deployment logs in Visual Studio's **Output** window

For issues or questions, check the deployment diagnostics under **Build > Publish > Settings > Diagnostics**.
