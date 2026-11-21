# API Configuration Guide

## Problem Fixed ✅

The Blazor WebAssembly client was hardcoded to use `https://localhost:7059` (local development) even in production, causing all API calls to fail.

## Solution Applied

### Updated Configuration Files

#### Production (src/QRBusinessExchange.Client/wwwroot/appsettings.json)
```json
{
  "ApiSettings": {
	"BaseUrl": "https://webapi.qrbound.com"
  }
}
```

#### Development (src/QRBusinessExchange.Client/wwwroot/appsettings.Development.json)
```json
{
  "ApiSettings": {
	"BaseUrl": "https://localhost:7059"
  }
}
```

## How It Works

The client's `Program.cs` reads the configuration:

```csharp
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7059";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
```

- **Development**: Uses localhost API when debugging locally
- **Production**: Uses `webapi.qrbound.com` when deployed to production

## Environment-Specific Behavior

### Local Development
```powershell
# Start local API on https://localhost:7059
dotnet run --project src/QRBusinessExchange.API

# Client uses appsettings.Development.json automatically in dev environment
```

### Production Deployment
- Client loads `appsettings.json` (production config)
- All API calls go to `https://webapi.qrbound.com`
- Ensure API is deployed to that URL

## Testing the Fix

### Before Publishing
1. Clear browser cache and local storage:
   - **F12 → Storage → Clear All**
2. Reload the page
3. Check **Console** for API connection success (no more ERR_CONNECTION_REFUSED)

### After Publishing
1. Deploy both projects:
   ```powershell
   # API
   dotnet publish src/QRBusinessExchange.API -c Release -p:PublishProfile=IISProfile

   # Web (Blazor)
   dotnet publish src/QRBusinessExchange.Web -c Release -p:PublishProfile=IISProfile
   ```

2. Test in browser:
   - Navigate to `https://www.qrbound.com`
   - Open **DevTools (F12)**
   - Check **Network** tab for API calls to `webapi.qrbound.com`
   - Should see successful API responses (200 status)

## Tracking Prevention (Non-Critical) ⚠️

You'll see messages like:
```
Tracking Prevention blocked access to storage for <URL>
```

This is normal browser behavior preventing third-party tracking cookies. To fix:

### If using IndexedDB (not affected):
✅ IndexedDB works fine (not blocked by Tracking Prevention)

### If using localStorage (might be blocked):
Change in JavaScript/C#:
```csharp
// Instead of localStorage for third-party sites
// Use IndexedDB (already implemented)
await IndexedDBService.SetAsync(key, value);
```

Or configure CORS with SameSite cookies:

```csharp
// In API Program.cs
services.AddCors(options =>
{
	options.AddPolicy("Production", policy =>
	{
		policy
			.WithOrigins("https://www.qrbound.com", "https://secured.incacorporation.com")
			.AllowAnyMethod()
			.AllowAnyHeader()
			.AllowCredentials();
	});
});
```

## CORS Configuration (if needed)

If API returns CORS errors, update `QRBusinessExchange.API/Program.cs`:

```csharp
app.UseCors("AllowWebClient");

app.MapCors("AllowWebClient"); // Add this before MapControllers
```

And in ConfigureServices:

```csharp
services.AddCors(options =>
{
	options.AddPolicy("AllowWebClient", policy =>
	{
		policy
			.WithOrigins(
				"https://www.qrbound.com",
				"https://secured.incacorporation.com",
				"https://localhost:3000",  // dev
				"https://localhost:7089"   // dev
			)
			.AllowAnyMethod()
			.AllowAnyHeader()
			.AllowCredentials();
	});
});
```

## Troubleshooting Checklist

- [ ] API is deployed and running on `webapi.qrbound.com`
- [ ] Client `appsettings.json` points to correct API URL
- [ ] CORS is configured on the API
- [ ] SSL certificates are valid (trusted) on both client and API
- [ ] Network tab shows API calls to correct domain
- [ ] Response status is 200 (not 301, 404, or 0)
- [ ] Cache cleared in browser (F12 → Storage → Clear All)
- [ ] Service Worker updated (hard refresh with Ctrl+Shift+R)

## Quick Reference

| Environment | Client Config | API Endpoint | Status |
|-----------|---------------|--------------|--------|
| Local Dev | appsettings.Development.json | https://localhost:7059 | ✅ Working |
| Production | appsettings.json | https://webapi.qrbound.com | ✅ Updated |

After publishing, verify with:
```bash
# Check API connectivity
curl https://webapi.qrbound.com/api/auth/health

# Should return 200 OK with pong response
```
