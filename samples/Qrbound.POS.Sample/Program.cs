using Qrbound.POS.Sample;
using Qrbound.POS.Sample.Components;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// The POS: SQL Server database from ConnectionStrings:QrboundPos (see appsettings.json), qrbound
// payments, staff sign-in. Secrets (service account password, setup key) go in user secrets.
builder.Services.AddQrboundPos(builder.Configuration, builder.Environment);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapQrboundPos();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(typeof(Qrbound.POS.QrboundPosOptions).Assembly);

await app.InitializeQrboundPosDatabaseAsync();
// A demo restaurant, menu and tables, only when asked for (Sample:SeedDemoData) and only in
// Development: never into a real restaurant's database.
if (app.Environment.IsDevelopment() && app.Configuration.GetValue<bool>("Sample:SeedDemoData"))
{
    await DemoData.SeedAsync(app.Services);
}

app.Run();
