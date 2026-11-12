# Blazor WebAssembly Loading Troubleshooting

## Issue: App Stuck on "Loading qrbound..." Screen

Your Blazor WASM app is not initializing. Here's how to diagnose and fix it.

---

## 🔍 Step 1: Check Browser Console (F12)

1. **Open Developer Tools**: Press `F12` in browser
2. **Go to Console Tab**: Look for red error messages
3. **Copy the exact error** and refer to the solutions below

### Common Errors & Solutions

#### Error 1: "Failed to fetch" or 404 errors
```
Failed to load: https://localhost:7059/...
```
**Solution**: API is not running or CORS is misconfigured
- Ensure API is running: `https://localhost:7059` should be accessible
- Check CORS headers in API response
- Restart API: `Ctrl+C`, then `F5` in Visual Studio

#### Error 2: "System.IO.FileNotFound" 
```
Could not find assembly 'QRBusinessExchange.Core'
```
**Solution**: Rebuild the solution
```powershell
cd H:\Users\gmach\source\.net core\10.0\Passkey\QRBusinessExchange
dotnet clean
dotnet build src/QRBusinessExchange.Client/QRBusinessExchange.Client.csproj
```

#### Error 3: Service Worker or Cache Issues
```
Service Worker error / Cache storage failed
```
**Solution**: Clear browser cache and hard refresh
```
F12 → Storage → Clear All
Ctrl+Shift+R (hard refresh without cache)
```

#### Error 4: "IndexedDB initialization failed"
```
IndexedDB not available / operation blocked
```
**Solution**: This is often a browser privacy setting. Try:
- Private/Incognito window (some browsers block IndexedDB in regular windows)
- Check browser privacy settings
- Use Edge or Chrome (Firefox may have stricter defaults)

---

## 🔍 Step 2: Check Network Tab

1. **Open F12 → Network Tab**
2. **Reload the page** (F5)
3. **Look for failed requests** (red X or 4xx/5xx status):
   - `qrbound` or `app.wasm` file download
   - API calls to `/api/...`
   - Static resources (CSS, JS, DLLs)

### What to look for:
- ✅ `app.wasm` - Should be 200 OK (1-5MB file)
- ✅ `dotnet.wasm` - Should be 200 OK
- ✅ `[filename].dll` files - Should be 200 OK (gzip compressed)
- ✅ API calls - Depends on app logic

### If you see 404 errors:
- **wwwroot files missing**: Run `dotnet publish`
- **API endpoint wrong**: Check `appsettings.json` for correct API URL

### If you see CORS errors:
API is blocking requests from browser. This requires CORS configuration in the API.

---

## 🔧 Step 3: Enable Verbose Console Logging

Added to your `Program.cs`:

```csharp
Console.WriteLine("[Startup] Application initializing...");
Console.WriteLine("[Startup] API Base URL: {apiBaseUrl}");
```

**To see these logs**:
1. Open F12 Console
2. Reload page
3. Look for `[Startup]` messages before the hang

These will help identify exactly where startup is failing.

---

## 🚀 Step 4: Quick Fixes to Try (in order)

### Fix 1: Clear Cache & Hard Refresh
```
F12 → Application → Clear Storage → Clear All
F5 (refresh)
```

### Fix 2: Restart Both Projects
```powershell
# Terminal 1: API
cd src/QRBusinessExchange.API
dotnet run

# Terminal 2: Web
cd src/QRBusinessExchange.Web
dotnet run
```

### Fix 3: Check API is Accessible
```powershell
# In PowerShell, verify API is running
$response = Invoke-WebRequest -Uri "https://localhost:7059" -SkipCertificateCheck
$response.StatusCode  # Should be 200
```

### Fix 4: Verify App Settings
```json
# src/QRBusinessExchange.Client/wwwroot/appsettings.json
{
  "ApiSettings": {
	"BaseUrl": "https://localhost:7059"
  }
}
```
✅ Should point to your API URL

### Fix 5: Rebuild WebAssembly Project
```powershell
dotnet clean src/QRBusinessExchange.Client/QRBusinessExchange.Client.csproj
dotnet build src/QRBusinessExchange.Client/QRBusinessExchange.Client.csproj -c Debug
```

---

## 📋 Step 5: Diagnostic Checklist

Before asking for help, verify:

- [ ] **Browser Console** - No red error messages (F12 → Console)
- [ ] **Network Tab** - No failed requests (F12 → Network, look for red X)
- [ ] **API Running** - `https://localhost:7059` accessible
- [ ] **Web Running** - `https://localhost:7090` shows loader screen
- [ ] **Config File** - `appsettings.json` has correct API URL
- [ ] **Build** - Solution builds with `dotnet build` (no errors)
- [ ] **Cache** - Cleared browser cache and hard refreshed (Ctrl+Shift+R)

---

## 🆘 If Still Stuck

**Capture this info**:

1. **Console Output** (F12 → Console tab):
   ```
   Copy all text → Paste in GitHub issue
   ```

2. **Network Failures** (F12 → Network tab):
   ```
   Look for red entries
   Click each → Copy full error
   ```

3. **API Status**:
   ```powershell
   curl https://localhost:7059/health -SkipCertificateCheck
   # Should return 200 OK
   ```

4. **Browser Info**:
   - Chrome/Edge/Firefox version?
   - Developer mode enabled?
   - Private/Incognito window?

---

## 📊 Application Architecture

Your app loads in this order:

```
1. Browser loads index.html
2. Downloads dotnet.wasm (~1MB)
3. Downloads app.wasm (~500KB)
4. Downloads dependency DLLs (~2-3MB)
5. CLR boots up in WebAssembly
6. Program.cs runs
7. Services initialize
8. Router works
9. Page renders
```

If it's stuck at step 3-5, it's a network issue.
If it's stuck at step 6-7, it's an initialization error.

---

## 🎯 Next Actions

1. **Clear cache + hard refresh**: Ctrl+Shift+R
2. **Check F12 Console for errors**: Look for red text
3. **Restart both API and Web**: Press F5 in VS
4. **Check Network tab**:  Look for failed `.wasm` or `.dll` downloads
5. **Share error message** if you still see the loader

You should see:
```
Loading qrbound...
[Startup] Application initializing...
[Startup] API Base URL: https://localhost:7059
[Startup] Registering services...
[Startup] Building host...
[Startup] Running application...
✅ App appears and works!
```

Let me know what errors you see in the console!
