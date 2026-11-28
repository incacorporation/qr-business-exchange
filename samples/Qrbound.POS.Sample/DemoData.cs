using Microsoft.EntityFrameworkCore;
using Qrbound.POS.Data;
using Qrbound.POS.Services;

namespace Qrbound.POS.Sample;

/// <summary>Development only: a demo restaurant with a small menu and six tables, once.</summary>
internal static class DemoData
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<PosDbContext>>();
        await using var db = await factory.CreateDbContextAsync();
        if (await db.Restaurants.AnyAsync()) return;

        var r = new Restaurant
        {
            Name = "Harbor Street Kitchen",
            Slug = "harbor-street",
            Description = "Burgers, bowls and fresh lemonade.",
            Currency = "USD",
            TaxRatePercent = 8.875m,
            TimeZoneId = "UTC"
        };
        db.Restaurants.Add(r);

        MenuCategory Cat(string name, int order) => new() { RestaurantId = r.Id, Name = name, SortOrder = order };
        MenuItem Item(MenuCategory c, string name, long cents, string? description, int order, params ModifierGroup[] groups) =>
            new() { RestaurantId = r.Id, CategoryId = c.Id, Name = name, PriceCents = cents, Description = description, SortOrder = order, ModifierGroups = groups.ToList() };
        ModifierGroup Group(string name, int min, int max, params (string Name, long Cents)[] choices) =>
            new() { Name = name, MinSelect = min, MaxSelect = max, Modifiers = choices.Select((c, i) => new Modifier { Name = c.Name, PriceDeltaCents = c.Cents, SortOrder = i }).ToList() };

        var mains = Cat("Mains", 1);
        var sides = Cat("Sides", 2);
        var drinks = Cat("Drinks", 3);
        db.MenuCategories.AddRange(mains, sides, drinks);

        db.MenuItems.AddRange(
            Item(mains, "Classic burger", 1350, "Beef patty, cheddar, pickles, house sauce.", 1,
                Group("Cook", 1, 1, ("Medium", 0), ("Medium well", 0), ("Well done", 0)),
                Group("Extras", 0, 3, ("Bacon", 200), ("Avocado", 175), ("Fried egg", 150))),
            Item(mains, "Crispy chicken sandwich", 1295, "Buttermilk chicken, slaw, hot honey.", 2),
            Item(mains, "Harvest bowl", 1225, "Quinoa, roast squash, kale, tahini.", 3,
                Group("Protein", 0, 1, ("Grilled chicken", 350), ("Tofu", 250), ("Salmon", 550))),
            Item(sides, "Fries", 495, "Sea salt.", 1, Group("Size", 1, 1, ("Regular", 0), ("Large", 200))),
            Item(sides, "Side salad", 550, "Greens, cucumber, lemon vinaigrette.", 2),
            Item(drinks, "Fresh lemonade", 450, null, 1, Group("Size", 1, 1, ("Regular", 0), ("Large", 150))),
            Item(drinks, "Iced tea", 375, "Unsweetened.", 2),
            Item(drinks, "Sparkling water", 300, null, 3));

        for (var n = 1; n <= 6; n++)
        {
            db.Tables.Add(new DiningTable { RestaurantId = r.Id, Name = $"Table {n}", Code = $"demo{n:00}", SortOrder = n });
        }

        await db.SaveChangesAsync();
    }
}
