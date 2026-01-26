using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using QRBusinessExchange.Client;
using QRBusinessExchange.Client.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Add root components
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Configure HttpClient for API calls
var apiBaseUrl = builder.Configuration["ApiSettings:BaseUrl"] ?? "https://localhost:7059";
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });

// Register services
builder.Services.AddScoped<ApiService>();
builder.Services.AddScoped<StateService>();
builder.Services.AddScoped<IndexedDBService>();
builder.Services.AddScoped<GeolocationService>();
builder.Services.AddScoped<PwaService>();

// Add authorization
builder.Services.AddAuthorizationCore();

await builder.Build().RunAsync();
