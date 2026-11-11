# Blazor WebAssembly Hybrid Migration Guide

## Overview
This guide documents the conversion from Blazor Server to Blazor WebAssembly Hybrid architecture.

## Architecture

### Projects:
- **QRBusinessExchange.API** - Backend API (unchanged)
- **QRBusinessExchange.Web** - Server host for WebAssembly client
- **QRBusinessExchange.Client** - NEW WebAssembly client application
- **QRBusinessExchange.Core** - Shared DTOs and models
- **QRBusinessExchange.Infrastructure** - Data access layer

## What Has Been Created

### 1. QRBusinessExchange.Client Project
- ? Project file (`.csproj`) with WebAssembly SDK
- ? `Program.cs` - Client startup configuration
- ? `App.razor` - Root component
- ? `_Imports.razor` - Global usings
- ? `wwwroot/index.html` - Entry HTML page
- ? `wwwroot/appsettings.json` - Client configuration

### 2. Client Services
- ? `ApiService.cs` - HTTP API client
- ? `StateService.cs` - Application state management (WebAssembly version with localStorage)
- ? `IndexedDBService.cs` - Browser storage wrapper

### 3. Client Layout
- ? `Layout/MainLayout.razor` - Main layout component

### 4. Server Updates
- ? Updated `QRBusinessExchange.Web.csproj` to reference Client project
- ? Updated `Program.cs` to host WebAssembly application

## Next Steps - Manual Actions Required

### Step 1: Copy Components from Server to Client

Copy these folders from `QRBusinessExchange.Web\Components` to `QRBusinessExchange.Client`:

```
Pages/
??? Login.razor
??? Register.razor
??? Dashboard.razor
??? Profile.razor
??? PublicProfile.razor
??? ... (all other pages)

Dashboard/
??? CreditDisplay.razor
??? SubscriptionBadge.razor
??? UsageGraph.razor
??? ... (all dashboard components)

Map/
??? LeafletMap.razor

Ads/
??? BannerAd.razor
```

### Step 2: Update Component Namespaces

In each copied component, update:
- **FROM**: `@using QRBusinessExchange.Web.Services`
- **TO**: `@using QRBusinessExchange.Client.Services`

Remove these Blazor Server-specific directives:
- `@rendermode InteractiveServer`
- `@inject RenderModeService` (not needed in WebAssembly)
- `@inject AuthenticationStateProvider` (if not using auth)

### Step 3: Update Login.razor for WebAssembly

Remove Blazor Server-specific code:
```csharp
// REMOVE these lines:
@inject RenderModeService RenderMode
@implements IAsyncDisposable
private bool _disposed = false;
private bool _isInteractive = false;
private bool _indexedDBEnabled = false;

// SIMPLIFY HandleLogin method - remove circuit disposal checks
```

### Step 4: Copy Static Assets

Copy these folders from `QRBusinessExchange.Web\wwwroot` to `QRBusinessExchange.Client\wwwroot`:

```
css/
js/
??? indexeddb-interop.js
??? chart-interop.js
??? leaflet-interop.js
??? qrcode-interop.js
bootstrap/
favicon.png
```

### Step 5: Update API CORS Settings

In `QRBusinessExchange.API\Program.cs`, add CORS for the WebAssembly client:

```csharp
// After: var builder = WebApplication.CreateBuilder(args);
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowWebAssemblyClient", policy =>
    {
        policy.WithOrigins("https://localhost:7089", "http://localhost:5116")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Before: app.Run();
app.UseCors("AllowWebAssemblyClient");
```

### Step 6: Update Client Configuration

Edit `QRBusinessExchange.Client\wwwroot\appsettings.json`:

```json
{
  "ApiSettings": {
    "BaseUrl": "https://localhost:7059"
  }
}
```

### Step 7: Add Models to Client

Create `QRBusinessExchange.Client\Models\ViewModels.cs` and copy content from `QRBusinessExchange.Web\Models\ViewModels.cs`.

### Step 8: Update Solution File

Add the Client project to your solution:

1. Right-click solution in Visual Studio
2. Add > Existing Project
3. Select `QRBusinessExchange.Client\QRBusinessExchange.Client.csproj`

### Step 9: Set Multiple Startup Projects

1. Right-click solution > Properties
2. Set Multiple startup projects:
   - QRBusinessExchange.API - Start
   - QRBusinessExchange.Web - Start

## Key Differences: Server vs WebAssembly

| Feature | Blazor Server | Blazor WebAssembly |
|---------|--------------|-------------------|
| **Execution** | Server-side | Client-side (browser) |
| **State** | Circuit-based | Browser-based (localStorage) |
| **Services** | Scoped per circuit | Scoped per app instance |
| **SignalR** | Required | Not needed |
| **Initial Load** | Fast | Slower (downloads .NET runtime) |
| **Offline** | No | Possible with PWA |
| **Security** | Server-protected | Client code visible |

## Benefits of Hybrid Approach

1. ? **Better Performance** - Client-side rendering reduces server load
2. ? **Scalability** - No SignalR circuits to maintain
3. ? **Offline Support** - Can implement PWA features
4. ? **CDN Deployment** - Static files can be served from CDN
5. ? **Reduced Server Costs** - Less server resources needed

## Authentication Considerations

The current implementation uses localStorage for state management. For production, consider:

1. **JWT Tokens** - Store in localStorage or sessionStorage
2. **HttpClient Interceptor** - Add authentication headers to API requests
3. **Token Refresh** - Implement token refresh logic
4. **Secure Storage** - Consider IndexedDB encryption for sensitive data

## Testing the Migration

1. Build solution: `dotnet build`
2. Run both API and Web projects
3. Navigate to: `https://localhost:7089`
4. The WebAssembly app should load
5. Test login functionality
6. Verify state persistence across page refreshes

## Troubleshooting

### Issue: Client doesn't load
- Check browser console for errors
- Verify `index.html` is being served
- Check that Client project is referenced in Web project

### Issue: API calls fail
- Verify CORS configuration
- Check API base URL in appsettings.json
- Inspect network tab for CORS errors

### Issue: State not persisting
- Check browser localStorage
- Verify StateService.InitializeAsync() is called
- Check console for JavaScript errors

## Rollback Plan

If needed, you can rollback by:
1. Remove Client project reference from Web.csproj
2. Restore original Web\Program.cs from git
3. Remove Blazor WebAssembly packages from Web project

## Files Created/Modified

### Created Files:
- src/QRBusinessExchange.Client/QRBusinessExchange.Client.csproj
- src/QRBusinessExchange.Client/Program.cs
- src/QRBusinessExchange.Client/App.razor
- src/QRBusinessExchange.Client/_Imports.razor
- src/QRBusinessExchange.Client/wwwroot/index.html
- src/QRBusinessExchange.Client/wwwroot/appsettings.json
- src/QRBusinessExchange.Client/Services/ApiService.cs
- src/QRBusinessExchange.Client/Services/StateService.cs
- src/QRBusinessExchange.Client/Services/IndexedDBService.cs
- src/QRBusinessExchange.Client/Layout/MainLayout.razor

### Modified Files:
- src/QRBusinessExchange.Web/QRBusinessExchange.Web.csproj
- src/QRBusinessExchange.Web/Program.cs

## Next Steps Summary

1. ? Copy all pages from Web/Components/Pages to Client/Pages
2. ? Copy all components to Client
3. ? Copy wwwroot assets to Client/wwwroot
4. ? Update namespaces in copied files
5. ? Add CORS to API
6. ? Add Client project to solution
7. ? Test and verify functionality
8. ? Remove old Blazor Server code from Web project (optional)

## Support

For questions or issues during migration:
1. Check browser console for errors
2. Review this migration guide
3. Check .NET 10 Blazor WebAssembly documentation
4. Verify all references and namespaces are correct
