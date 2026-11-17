using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Qrbound.SignIn.Sample;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Only public values here: a browser app is a public client and proves itself with PKCE.
// Never put a client secret in a WebAssembly app.
builder.Services.AddQrboundSignIn(
    options => builder.Configuration.GetSection("Qrbound").Bind(options),
    useAuthenticationStateProvider: true);

await builder.Build().RunAsync();
