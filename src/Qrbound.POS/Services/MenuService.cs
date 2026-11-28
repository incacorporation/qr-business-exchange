using Microsoft.EntityFrameworkCore;
using Qrbound.POS.Data;

namespace Qrbound.POS.Services;

/// <summary>The menu: categories, items and their modifier groups.</summary>
public sealed class MenuService(IDbContextFactory<PosDbContext> dbFactory)
{
    /// <summary>
    /// The menu, in display order. <paramref name="sellableOnly"/>: only visible categories and
    /// available items (what customers and the register see); otherwise everything not archived.
    /// </summary>
    public async Task<List<MenuCategory>> GetMenuAsync(Guid restaurantId, bool sellableOnly)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var categories = await db.MenuCategories.AsNoTracking()
            .Where(c => c.RestaurantId == restaurantId && (!sellableOnly || c.IsVisible))
            .Include(c => c.Items.Where(i => !i.IsArchived && (!sellableOnly || i.IsAvailable)))
                .ThenInclude(i => i.ModifierGroups)
                    .ThenInclude(g => g.Modifiers)
            .AsSplitQuery()
            .ToListAsync();

        foreach (var c in categories)
        {
            c.Items = c.Items.OrderBy(i => i.SortOrder).ThenBy(i => i.Name).ToList();
            foreach (var i in c.Items)
            {
                i.ModifierGroups = i.ModifierGroups.OrderBy(g => g.SortOrder).ToList();
                foreach (var g in i.ModifierGroups) g.Modifiers = g.Modifiers.OrderBy(m => m.SortOrder).ToList();
            }
        }

        return categories
            .Where(c => !sellableOnly || c.Items.Count > 0)
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToList();
    }

    // ---- Categories ----

    public async Task<MenuCategory> SaveCategoryAsync(Guid restaurantId, Guid? id, string name, string? description, bool isVisible)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new PosException("Give the category a name.");
        await using var db = await dbFactory.CreateDbContextAsync();

        MenuCategory c;
        if (id is { } existing)
        {
            c = await db.MenuCategories.FirstOrDefaultAsync(x => x.Id == existing && x.RestaurantId == restaurantId)
                ?? throw new PosException("Category not found.");
        }
        else
        {
            var last = await db.MenuCategories.Where(x => x.RestaurantId == restaurantId).Select(x => (int?)x.SortOrder).MaxAsync() ?? 0;
            c = new MenuCategory { RestaurantId = restaurantId, SortOrder = last + 1 };
            db.MenuCategories.Add(c);
        }

        c.Name = name.Trim();
        c.Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        c.IsVisible = isVisible;
        await db.SaveChangesAsync();
        return c;
    }

    /// <summary>Deletes a category and its items; refused while it has items, so nothing goes by accident.</summary>
    public async Task DeleteCategoryAsync(Guid restaurantId, Guid categoryId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        if (await db.MenuItems.AnyAsync(i => i.CategoryId == categoryId && !i.IsArchived))
        {
            throw new PosException("Move or remove the category's items first.");
        }

        await db.MenuItems.Where(i => i.CategoryId == categoryId).ExecuteDeleteAsync();
        await db.MenuCategories.Where(c => c.Id == categoryId && c.RestaurantId == restaurantId).ExecuteDeleteAsync();
    }

    /// <summary>Moves a category one place up (-1) or down (+1).</summary>
    public async Task MoveCategoryAsync(Guid restaurantId, Guid categoryId, int direction)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.MenuCategories.Where(c => c.RestaurantId == restaurantId).OrderBy(c => c.SortOrder).ThenBy(c => c.Name).ToListAsync();
        Move(all, all.FindIndex(c => c.Id == categoryId), direction, (c, n) => c.SortOrder = n);
        await db.SaveChangesAsync();
    }

    // ---- Items ----

    /// <summary>
    /// Creates or updates an item with its modifier groups. Groups and modifiers not in
    /// <paramref name="item"/> are removed; ones with a known Id are updated.
    /// </summary>
    public async Task SaveItemAsync(Guid restaurantId, MenuItem item)
    {
        Validate(item);
        await using var db = await dbFactory.CreateDbContextAsync();

        if (!await db.MenuCategories.AnyAsync(c => c.Id == item.CategoryId && c.RestaurantId == restaurantId))
        {
            throw new PosException("Pick a category.");
        }

        var stored = await db.MenuItems.Include(i => i.ModifierGroups).ThenInclude(g => g.Modifiers)
            .FirstOrDefaultAsync(i => i.Id == item.Id && i.RestaurantId == restaurantId);

        if (stored is null)
        {
            var last = await db.MenuItems.Where(i => i.CategoryId == item.CategoryId).Select(i => (int?)i.SortOrder).MaxAsync() ?? 0;
            stored = new MenuItem { Id = item.Id == Guid.Empty ? Guid.NewGuid() : item.Id, RestaurantId = restaurantId, SortOrder = last + 1 };
            db.MenuItems.Add(stored);
        }

        stored.CategoryId = item.CategoryId;
        stored.Name = item.Name.Trim();
        stored.Description = string.IsNullOrWhiteSpace(item.Description) ? null : item.Description.Trim();
        stored.PriceCents = item.PriceCents;
        stored.ImageUrl = string.IsNullOrWhiteSpace(item.ImageUrl) ? null : item.ImageUrl.Trim();
        stored.IsAvailable = item.IsAvailable;

        // Modifier groups: replace with the submitted set, keeping Ids.
        var keepGroups = item.ModifierGroups.Select(g => g.Id).ToHashSet();
        foreach (var gone in stored.ModifierGroups.Where(g => !keepGroups.Contains(g.Id)).ToList())
        {
            db.ModifierGroups.Remove(gone);
        }

        var gOrder = 0;
        foreach (var g in item.ModifierGroups)
        {
            var sg = stored.ModifierGroups.FirstOrDefault(x => x.Id == g.Id);
            if (sg is null)
            {
                sg = new ModifierGroup { Id = g.Id == Guid.Empty ? Guid.NewGuid() : g.Id };
                stored.ModifierGroups.Add(sg);
            }

            sg.Name = g.Name.Trim();
            sg.MinSelect = g.MinSelect;
            sg.MaxSelect = g.MaxSelect;
            sg.SortOrder = ++gOrder;

            var keepMods = g.Modifiers.Select(m => m.Id).ToHashSet();
            foreach (var gone in sg.Modifiers.Where(m => !keepMods.Contains(m.Id)).ToList())
            {
                db.Modifiers.Remove(gone);
            }

            var mOrder = 0;
            foreach (var m in g.Modifiers)
            {
                var sm = sg.Modifiers.FirstOrDefault(x => x.Id == m.Id);
                if (sm is null)
                {
                    sm = new Modifier { Id = m.Id == Guid.Empty ? Guid.NewGuid() : m.Id };
                    sg.Modifiers.Add(sm);
                }

                sm.Name = m.Name.Trim();
                sm.PriceDeltaCents = m.PriceDeltaCents;
                sm.SortOrder = ++mOrder;
            }
        }

        await db.SaveChangesAsync();
    }

    public async Task SetAvailableAsync(Guid restaurantId, Guid itemId, bool available)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.MenuItems.Where(i => i.Id == itemId && i.RestaurantId == restaurantId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsAvailable, available));
    }

    /// <summary>Removes an item from the menu. Past orders keep their copy of it.</summary>
    public async Task ArchiveItemAsync(Guid restaurantId, Guid itemId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.MenuItems.Where(i => i.Id == itemId && i.RestaurantId == restaurantId)
            .ExecuteUpdateAsync(s => s.SetProperty(i => i.IsArchived, true));
    }

    public async Task MoveItemAsync(Guid restaurantId, Guid itemId, int direction)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.MenuItems.FirstOrDefaultAsync(i => i.Id == itemId && i.RestaurantId == restaurantId);
        if (item is null) return;
        var siblings = await db.MenuItems.Where(i => i.CategoryId == item.CategoryId && !i.IsArchived)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Name).ToListAsync();
        Move(siblings, siblings.FindIndex(i => i.Id == itemId), direction, (i, n) => i.SortOrder = n);
        await db.SaveChangesAsync();
    }

    private static void Move<T>(List<T> list, int index, int direction, Action<T, int> setOrder)
    {
        var target = index + Math.Sign(direction);
        if (index >= 0 && target >= 0 && target < list.Count)
        {
            (list[index], list[target]) = (list[target], list[index]);
        }

        for (var n = 0; n < list.Count; n++) setOrder(list[n], n + 1);
    }

    private static void Validate(MenuItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Name)) throw new PosException("Give the item a name.");
        if (item.PriceCents is < 0 or > 10_000_00) throw new PosException("The price must be between 0 and 10,000.");

        foreach (var g in item.ModifierGroups)
        {
            if (string.IsNullOrWhiteSpace(g.Name)) throw new PosException("Every option group needs a name.");
            if (g.Modifiers.Count == 0) throw new PosException($"\"{g.Name}\" needs at least one choice.");
            if (g.Modifiers.Any(m => string.IsNullOrWhiteSpace(m.Name))) throw new PosException($"Every choice in \"{g.Name}\" needs a name.");
            if (g.Modifiers.Any(m => m.PriceDeltaCents is < 0 or > 1_000_00)) throw new PosException($"Choice prices in \"{g.Name}\" must be between 0 and 1,000.");
            if (g.MinSelect < 0 || g.MaxSelect < 1 || g.MinSelect > g.MaxSelect || g.MinSelect > g.Modifiers.Count)
            {
                throw new PosException($"\"{g.Name}\": pick at least {g.MinSelect} and at most {g.MaxSelect} doesn't work with {g.Modifiers.Count} choice(s).");
            }
        }
    }
}
