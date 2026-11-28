using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Qrbound.POS.Data;

namespace Qrbound.POS.Services;

/// <summary>What a customer or the register sends to place an order.</summary>
public sealed class PlaceOrderRequest
{
    public Guid RestaurantId { get; set; }

    /// <summary>The table's QR code (customer) or the table picked on the register; null for takeaway.</summary>
    public Guid? TableId { get; set; }

    public OrderSource Source { get; set; }
    public string? CustomerName { get; set; }

    /// <summary>The signed-in qrbound customer placing the order (phone orders).</summary>
    public PosCustomer? Customer { get; set; }
    public string? Notes { get; set; }
    public List<CartLine> Lines { get; set; } = new();

    /// <summary>Staff member's name, for register orders.</summary>
    public string? CreatedBy { get; set; }
}

/// <summary>Placing orders, pricing carts and moving orders through the kitchen.</summary>
public sealed class OrderService(IDbContextFactory<PosDbContext> dbFactory, PosEvents events, TimeProvider clock,
    Microsoft.Extensions.Options.IOptions<QrboundPosOptions> options)
{
    public const int MaxLines = 50;
    public const int MaxQuantity = 50;

    /// <summary>
    /// Prices a cart from the current menu (never from prices the browser sent) and checks every
    /// modifier choice. Throws <see cref="PosException"/> for anything that can't be ordered.
    /// </summary>
    public async Task<List<OrderLine>> PriceAsync(PosDbContext db, Guid restaurantId, IReadOnlyList<CartLine> cart)
    {
        if (cart.Count == 0) throw new PosException("The order is empty.");
        if (cart.Count > MaxLines) throw new PosException($"An order can have at most {MaxLines} lines.");

        var itemIds = cart.Select(l => l.MenuItemId).Distinct().ToList();
        var items = await db.MenuItems.AsNoTracking()
            .Where(i => itemIds.Contains(i.Id) && i.RestaurantId == restaurantId)
            .Include(i => i.Category)
            .Include(i => i.ModifierGroups).ThenInclude(g => g.Modifiers)
            .AsSplitQuery()
            .ToDictionaryAsync(i => i.Id);

        var lines = new List<OrderLine>();
        var sort = 0;
        foreach (var c in cart)
        {
            if (!items.TryGetValue(c.MenuItemId, out var item) || item.IsArchived || !item.IsAvailable || item.Category is not { IsVisible: true })
            {
                throw new PosException($"{(string.IsNullOrEmpty(c.Name) ? "An item" : c.Name)} isn't available any more. Remove it to continue.");
            }

            if (c.Quantity is < 1 or > MaxQuantity) throw new PosException($"Quantity of {item.Name} must be 1 to {MaxQuantity}.");

            var chosen = new List<Modifier>();
            var picked = c.ModifierIds.Distinct().ToHashSet();
            foreach (var g in item.ModifierGroups.OrderBy(g => g.SortOrder))
            {
                var inGroup = g.Modifiers.Where(m => picked.Contains(m.Id)).OrderBy(m => m.SortOrder).ToList();
                if (inGroup.Count < g.MinSelect) throw new PosException($"{item.Name}: choose {(g.MinSelect == 1 ? "a" : g.MinSelect.ToString())} {g.Name.ToLowerInvariant()}.");
                if (inGroup.Count > g.MaxSelect) throw new PosException($"{item.Name}: choose at most {g.MaxSelect} {g.Name.ToLowerInvariant()}.");
                chosen.AddRange(inGroup);
                picked.ExceptWith(inGroup.Select(m => m.Id));
            }

            if (picked.Count > 0) throw new PosException($"{item.Name}: a choice isn't available any more. Choose again.");

            var unit = item.PriceCents + chosen.Sum(m => m.PriceDeltaCents);
            var notes = string.IsNullOrWhiteSpace(c.Notes) ? null : c.Notes.Trim();
            if (notes is { Length: > 200 }) notes = notes[..200];

            lines.Add(new OrderLine
            {
                MenuItemId = item.Id,
                Name = item.Name,
                Quantity = c.Quantity,
                UnitPriceCents = unit,
                LineTotalCents = unit * c.Quantity,
                ModifiersText = chosen.Count == 0 ? null : string.Join(", ", chosen.Select(m => m.Name)),
                Notes = notes,
                SortOrder = ++sort
            });
        }

        return lines;
    }

    /// <summary>Places an order. Customer orders of a pay-first restaurant wait for payment before the kitchen sees them.</summary>
    public async Task<Order> PlaceAsync(PlaceOrderRequest request)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var restaurant = await db.Restaurants.AsNoTracking().FirstOrDefaultAsync(r => r.Id == request.RestaurantId)
            ?? throw new PosException("Restaurant not found.");

        if (request.Source == OrderSource.Customer && request.Customer is null && options.Value.CustomerSignIn.Required)
        {
            throw new PosException("Sign in with your qrbound account to order.");
        }

        if (request.Source == OrderSource.Customer && !restaurant.OnlineOrderingOpen)
        {
            throw new PosException($"{restaurant.Name} isn't taking orders from phones right now. Please ask a member of staff.");
        }

        if (request.TableId is { } tableId)
        {
            if (!await db.Tables.AnyAsync(t => t.Id == tableId && t.RestaurantId == restaurant.Id && t.IsActive))
            {
                throw new PosException("That table isn't taking orders. Please ask a member of staff.");
            }
        }
        else if (request.Source == OrderSource.Customer && !restaurant.AllowTakeaway)
        {
            throw new PosException("Scan the QR code on your table to order.");
        }

        var lines = await PriceAsync(db, restaurant.Id, request.Lines);
        var subtotal = lines.Sum(l => l.LineTotalCents);
        var tax = Money.Tax(subtotal, restaurant.TaxRatePercent);
        var now = clock.GetUtcNow().UtcDateTime;
        var toKitchenNow = request.Source == OrderSource.Register || !restaurant.PayBeforeKitchen;

        var name = !string.IsNullOrWhiteSpace(request.CustomerName) ? request.CustomerName.Trim()
            : request.Source == OrderSource.Customer ? request.Customer?.Name?.Trim()
            : null;
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        var order = new Order
        {
            RestaurantId = restaurant.Id,
            TableId = request.TableId,
            BusinessDate = RestaurantService.BusinessDate(restaurant, now),
            PublicToken = Tokens.New(),
            Source = request.Source,
            Status = toKitchenNow ? OrderStatus.New : OrderStatus.AwaitingPayment,
            SentToKitchenAt = toKitchenNow ? now : null,
            CustomerName = name is { Length: > 80 } ? name[..80] : name,
            CustomerUserId = request.Source == OrderSource.Customer ? request.Customer?.Subject : null,
            CustomerEmail = request.Source == OrderSource.Customer ? Truncate(request.Customer?.Email, 254) : null,
            Notes = notes is { Length: > 500 } ? notes[..500] : notes,
            SubtotalCents = subtotal,
            TaxRatePercent = restaurant.TaxRatePercent,
            TaxCents = tax,
            TotalCents = subtotal + tax,
            Currency = restaurant.Currency,
            CreatedAt = now,
            CreatedBy = request.CreatedBy,
            Lines = lines
        };

        // The day's ticket number: next after the highest. Two orders racing for it hit the unique
        // index; the loser takes the next number.
        for (var attempt = 0; ; attempt++)
        {
            order.Number = 1 + (await db.Orders.Where(o => o.RestaurantId == restaurant.Id && o.BusinessDate == order.BusinessDate)
                .Select(o => (int?)o.Number).MaxAsync() ?? 0);
            db.Orders.Add(order);
            try
            {
                await db.SaveChangesAsync();
                break;
            }
            catch (DbUpdateException ex) when (attempt < 5 && ex.InnerException is SqlException { Number: 2601 or 2627 })
            {
                db.ChangeTracker.Clear();
            }
        }

        events.Publish(order.RestaurantId, order.Id);
        return order;
    }

    public async Task<Order?> GetAsync(Guid orderId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Full(db).FirstOrDefaultAsync(o => o.Id == orderId);
    }

    /// <summary>The customer's view of an order, by the token in their link.</summary>
    public async Task<Order?> GetByTokenAsync(Guid restaurantId, string token)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Full(db).FirstOrDefaultAsync(o => o.RestaurantId == restaurantId && o.PublicToken == token);
    }

    /// <summary>
    /// The customer's view of an order. With customer sign-in on, an order belongs to one qrbound
    /// user: someone else gets null, as if there were no such order. A register order nobody owns
    /// yet goes to the first signed-in customer who opens its pay link.
    /// </summary>
    public async Task<Order?> GetForCustomerAsync(Guid restaurantId, string token, PosCustomer? customer)
    {
        var order = await GetByTokenAsync(restaurantId, token);
        if (order is null || !options.Value.CustomerSignIn.Required) return order;
        if (customer is null) return null;
        if (order.CustomerUserId == customer.Subject) return order;
        if (order.CustomerUserId is not null) return null;

        var subject = customer.Subject;
        var email = Truncate(customer.Email, 254);
        var name = Truncate(customer.Name, 80);
        await using var db = await dbFactory.CreateDbContextAsync();
        var claimed = await db.Orders
            .Where(o => o.Id == order.Id && o.CustomerUserId == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(o => o.CustomerUserId, subject)
                .SetProperty(o => o.CustomerEmail, email)
                .SetProperty(o => o.CustomerName, o => o.CustomerName ?? name));
        if (claimed == 0) return null; // someone else claimed it a moment ago

        events.Publish(order.RestaurantId, order.Id);
        return await GetByTokenAsync(restaurantId, token);
    }

    private static string? Truncate(string? s, int max) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim() is { } t && t.Length > max ? t[..max] : s.Trim();

    /// <summary>The kitchen queue: orders sent to the kitchen and not yet completed, oldest first.</summary>
    public async Task<List<Order>> KitchenAsync(Guid restaurantId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await Full(db)
            .Where(o => o.RestaurantId == restaurantId && (o.Status == OrderStatus.New || o.Status == OrderStatus.Preparing || o.Status == OrderStatus.Ready))
            .OrderBy(o => o.SentToKitchenAt).ThenBy(o => o.Number)
            .ToListAsync();
    }

    /// <summary>Orders of a business day, newest first, optionally only unpaid ones.</summary>
    public async Task<List<Order>> ListAsync(Guid restaurantId, DateOnly businessDate, bool unpaidOnly = false)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var q = Full(db).Where(o => o.RestaurantId == restaurantId && o.BusinessDate == businessDate);
        if (unpaidOnly) q = q.Where(o => o.PaymentStatus != PaymentStatus.Paid && o.Status != OrderStatus.Cancelled);
        return await q.OrderByDescending(o => o.Number).ToListAsync();
    }

    /// <summary>Totals of a business day: paid orders, their sales, tax and tips.</summary>
    public async Task<DaySummary> SummaryAsync(Guid restaurantId, DateOnly businessDate)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var paid = db.Orders.Where(o => o.RestaurantId == restaurantId && o.BusinessDate == businessDate && o.PaymentStatus == PaymentStatus.Paid);
        return new DaySummary
        {
            PaidOrders = await paid.CountAsync(),
            SalesCents = await paid.SumAsync(o => (long?)o.SubtotalCents) ?? 0,
            TaxCents = await paid.SumAsync(o => (long?)o.TaxCents) ?? 0,
            TipsCents = await paid.SumAsync(o => (long?)o.TipCents) ?? 0,
            CardCents = await paid.Where(o => o.PaymentMethod == PaymentMethod.Qrbound).SumAsync(o => (long?)(o.PaidCents ?? o.TotalCents)) ?? 0,
            CashCents = await paid.Where(o => o.PaymentMethod == PaymentMethod.Cash).SumAsync(o => (long?)o.TotalCents) ?? 0,
            OpenOrders = await db.Orders.CountAsync(o => o.RestaurantId == restaurantId && o.BusinessDate == businessDate
                && o.Status != OrderStatus.Cancelled && (o.Status != OrderStatus.Completed || o.PaymentStatus != PaymentStatus.Paid))
        };
    }

    /// <summary>Moves an order along the kitchen: New → Preparing → Ready → Completed.</summary>
    public async Task SetStatusAsync(Guid orderId, OrderStatus status)
    {
        await UpdateAsync(orderId, o =>
        {
            if (o.Status is OrderStatus.Cancelled) throw new PosException($"Order {o.Number} was cancelled.");
            if (o.Status is OrderStatus.AwaitingPayment && status != OrderStatus.New)
            {
                throw new PosException($"Order {o.Number} isn't paid yet. Send it to the kitchen first.");
            }

            var now = clock.GetUtcNow().UtcDateTime;
            o.Status = status;
            switch (status)
            {
                case OrderStatus.New: o.SentToKitchenAt ??= now; break;
                case OrderStatus.Ready: o.ReadyAt = now; break;
                case OrderStatus.Completed: o.CompletedAt = now; break;
            }
        });
    }

    /// <summary>Cancels an order. A paid order is refused: refund it in CardPointe first, then cancel.</summary>
    public async Task CancelAsync(Guid orderId)
    {
        await UpdateAsync(orderId, o =>
        {
            if (o.PaymentStatus == PaymentStatus.Paid && o.PaymentMethod != PaymentMethod.Cash)
            {
                throw new PosException($"Order {o.Number} is paid by card. Refund it in CardPointe (reference {o.TransactionId}) before cancelling.");
            }

            if (o.Status == OrderStatus.Completed) throw new PosException($"Order {o.Number} is already completed.");
            o.Status = OrderStatus.Cancelled;
            o.CancelledAt = clock.GetUtcNow().UtcDateTime;
        });
    }

    /// <summary>Staff took cash for the order.</summary>
    public async Task MarkPaidCashAsync(Guid orderId)
    {
        await UpdateAsync(orderId, o =>
        {
            if (o.PaymentStatus == PaymentStatus.Paid) throw new PosException($"Order {o.Number} is already paid.");
            if (o.PaymentStatus == PaymentStatus.NeedsReview)
            {
                throw new PosException($"A card payment for order {o.Number} couldn't be confirmed. Check invoice {o.PaymentInvoiceNumber} in CardPointe before taking cash.");
            }

            if (o.Status == OrderStatus.Cancelled) throw new PosException($"Order {o.Number} was cancelled.");
            MarkPaid(o, PaymentMethod.Cash, o.TotalCents, 0, null, clock.GetUtcNow().UtcDateTime);
        });
    }

    /// <summary>Records a payment and, for a pay-first order, sends it to the kitchen.</summary>
    internal static void MarkPaid(Order o, PaymentMethod method, long paidCents, long tipCents, string? transactionId, DateTime now)
    {
        o.PaymentStatus = PaymentStatus.Paid;
        o.PaymentMethod = method;
        o.PaidCents = paidCents;
        o.TipCents = tipCents;
        o.TransactionId = transactionId;
        o.PaidAt = now;
        o.PaymentError = null;
        if (o.Status == OrderStatus.AwaitingPayment)
        {
            o.Status = OrderStatus.New;
            o.SentToKitchenAt = now;
        }
    }

    /// <summary>Loads, changes and saves an order, retrying once if someone else changed it meanwhile.</summary>
    internal async Task UpdateAsync(Guid orderId, Action<Order> change)
    {
        for (var attempt = 0; ; attempt++)
        {
            await using var db = await dbFactory.CreateDbContextAsync();
            var order = await db.Orders.FirstOrDefaultAsync(o => o.Id == orderId) ?? throw new PosException("Order not found.");
            change(order);
            try
            {
                await db.SaveChangesAsync();
                events.Publish(order.RestaurantId, order.Id);
                return;
            }
            catch (DbUpdateConcurrencyException) when (attempt < 3)
            {
                // Reload and apply the change to the fresh copy.
            }
        }
    }

    private static IQueryable<Order> Full(PosDbContext db) =>
        db.Orders.AsNoTracking().Include(o => o.Lines.OrderBy(l => l.SortOrder)).Include(o => o.Table).Include(o => o.Restaurant);
}

public sealed class DaySummary
{
    public int PaidOrders { get; init; }
    public long SalesCents { get; init; }
    public long TaxCents { get; init; }
    public long TipsCents { get; init; }
    public long CardCents { get; init; }
    public long CashCents { get; init; }
    public int OpenOrders { get; init; }
}
