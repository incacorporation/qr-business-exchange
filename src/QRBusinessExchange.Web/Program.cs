using Microsoft.AspNetCore.Components.Authorization;
using QRBusinessExchange.Web.Components;
using QRBusinessExchange.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services for hosting Blazor WebAssembly
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Serve WebAssembly framework files FIRST (from Client project)
app.UseBlazorFrameworkFiles();

// Then serve static files (Client wwwroot takes precedence)
app.UseStaticFiles();

app.UseRouting();

app.MapRazorPages();
app.MapControllers();

// Fallback to index.html for client-side routing
app.MapFallbackToFile("index.html");

app.Run();
