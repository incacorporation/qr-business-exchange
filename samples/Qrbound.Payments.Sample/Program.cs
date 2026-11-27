using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Qrbound.Payments.Sample;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// The qrbound API that holds the CardPointe credentials and charges the cards. Only its address
// lives here: a browser app never sees gateway credentials.
var apiBaseUrl = builder.Configuration["QrboundPayments:ApiBaseUrl"]
    ?? throw new InvalidOperationException("Set QrboundPayments:ApiBaseUrl in wwwroot/appsettings.json.");

builder.Services.AddQrboundPayments(new Uri(apiBaseUrl.TrimEnd('/') + "/"));

await builder.Build().RunAsync();
