using System.Text;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using mlm.qrbound.com.Components;
using mlm.qrbound.com.Data;
using mlm.qrbound.com.Domain;
using mlm.qrbound.com.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<MlmDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Mlm") ?? "Data Source=mlm.db"));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton(builder.Configuration.GetSection("Compensation").Get<CompensationSettings>() ?? new CompensationSettings());
builder.Services.AddSingleton<CompensationEngine>();
builder.Services.AddSingleton(builder.Configuration.GetSection("Ai").Get<AiOptions>() ?? new AiOptions());
builder.Services.AddSingleton<AiAssistant>();
builder.Services.AddScoped<NetworkService>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddResponseCompression(o => o.EnableForHttps = true);
builder.Services.AddOutputCache(o => o.AddPolicy("seo", p => p.Expire(TimeSpan.FromMinutes(30))));

var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<MlmDbContext>>().CreateDbContextAsync();
    await SeedData.EnsureSeededAsync(db);
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseOutputCache();
app.UseAntiforgery();

app.MapStaticAssets();

var siteUrl = (app.Configuration["Site:BaseUrl"] ?? "https://mlm.qrbound.com").TrimEnd('/');

app.MapGet("/robots.txt", () => Results.Text(
    $"""
    User-agent: *
    Allow: /
    Disallow: /admin

    Sitemap: {siteUrl}/sitemap.xml
    """, "text/plain")).CacheOutput("seo");

app.MapGet("/sitemap.xml", async (NetworkService network) =>
{
    XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
    var urls = new List<(string Path, string Priority)>
    {
        ("/", "1.0"), ("/products", "0.9"), ("/opportunity", "0.7"),
        ("/income-disclosure", "0.6"), ("/faq", "0.6"),
    };
    urls.AddRange((await network.GetProductsAsync()).Select(p => ($"/products/{p.Slug}", "0.8")));
    urls.AddRange((await network.GetMembersAsync()).Where(m => m.IsActiveAccount && m.SponsorId != null).Select(m => ($"/r/{m.Slug}", "0.5")));

    var doc = new XDocument(new XElement(ns + "urlset",
        urls.Select(u => new XElement(ns + "url",
            new XElement(ns + "loc", siteUrl + u.Path),
            new XElement(ns + "priority", u.Priority)))));
    return Results.Text(doc.Declaration + doc.ToString(), "application/xml", Encoding.UTF8);
}).CacheOutput("seo");

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
