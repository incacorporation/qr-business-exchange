using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using QRBusinessExchange.Client;
using QRBusinessExchange.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

Console.WriteLine("[Startup] Application initializing...");

// Add root components
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for API calls
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7059";
Console.WriteLine($"[Startup] API Base URL: {apiBaseUrl}");
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Register services
Console.WriteLine("[Startup] Registering services...");
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<StateService>();
builder.Services.AddScoped<IndexedDBService>();
builder.Services.AddScoped<GeolocationService>();
builder.Services.AddScoped<PwaService>();
builder.Services.AddScoped<WebAuthnService>();
builder.Services.AddScoped<PortalAuthService>();

// Add authorization
builder.Services.AddAuthorizationCore();

try
{
    Console.WriteLine("[Startup] Building host...");
    var host = builder.Build();
    Console.WriteLine("[Startup] Running application...");
    await host.RunAsync();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"[Startup] FATAL ERROR: {ex.Message}");
    Console.Error.WriteLine($"[Startup] Stack Trace: {ex.StackTrace}");
    throw;
}
