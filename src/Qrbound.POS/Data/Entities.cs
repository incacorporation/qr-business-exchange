namespace Qrbound.POS.Data;

// Money is always whole cents (long), as in Qrbound.Payments: $12.50 is 1250.

/// <summary>A restaurant (location) run from this POS. One install can run several.</summary>
public class Restaurant
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    /// <summary>Short, URL-safe name used in the customer links: /r/{slug}.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }
    public string? LogoUrl { get; set; }

    /// <summary>ISO currency code; must match the qrbound business's currency.</summary>
    public string Currency { get; set; } = "USD";

    /// <summary>Sales tax in percent of the subtotal (e.g. 8.875). Menu prices exclude tax.</summary>
    public decimal TaxRatePercent { get; set; }

    /// <summary>The qrbound business that takes this restaurant's card payments.</summary>
    public Guid? QrboundBusinessId { get; set; }

    /// <summary>Customers can order from their phones right now.</summary>
    public bool OnlineOrderingOpen { get; set; } = true;

    /// <summary>
    /// True: a customer's order reaches the kitchen only once it's paid. False: it goes to the kitchen
    /// straight away and the customer pays when they're ready (dine-in tab).
    /// </summary>
    public bool PayBeforeKitchen { get; set; } = true;

    /// <summary>Customers ordering without a table (takeaway / pickup at the counter) are accepted.</summary>
    public bool AllowTakeaway { get; set; } = true;

    /// <summary>The time zone the restaurant's business day runs in (Windows or IANA ID).</summary>
    public string TimeZoneId { get; set; } = "UTC";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MenuCategory> Categories { get; set; } = new();
    public List<DiningTable> Tables { get; set; } = new();
}

public class MenuCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public bool IsVisible { get; set; } = true;

    public Restaurant? Restaurant { get; set; }
    public List<MenuItem> Items { get; set; } = new();
}

public class MenuItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public long PriceCents { get; set; }
    public string? ImageUrl { get; set; }

    /// <summary>False hides the item from customers and the register (e.g. sold out today).</summary>
    public bool IsAvailable { get; set; } = true;

    /// <summary>Soft delete: past orders keep pointing at the item.</summary>
    public bool IsArchived { get; set; }

    public int SortOrder { get; set; }

    public MenuCategory? Category { get; set; }
    public List<ModifierGroup> ModifierGroups { get; set; } = new();
}

/// <summary>Choices on an item, e.g. "Size" (pick 1) or "Extras" (pick up to 3).</summary>
public class ModifierGroup
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>At least this many must be picked (1 makes the group required).</summary>
    public int MinSelect { get; set; }

    /// <summary>At most this many can be picked.</summary>
    public int MaxSelect { get; set; } = 1;

    public int SortOrder { get; set; }

    public MenuItem? MenuItem { get; set; }
    public List<Modifier> Modifiers { get; set; } = new();
}

public class Modifier
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ModifierGroupId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Added to the item's price (0 for a free choice).</summary>
    public long PriceDeltaCents { get; set; }

    public int SortOrder { get; set; }

    public ModifierGroup? Group { get; set; }
}

/// <summary>A table with its own QR code. The code in the link is random, so links can't be guessed.</summary>
public class DiningTable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Random token in the table's link: /r/{slug}/t/{code}. Replace it to retire printed codes.</summary>
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public Restaurant? Restaurant { get; set; }
}

public enum OrderStatus
{
    /// <summary>Placed by a customer of a pay-first restaurant; not in the kitchen until paid.</summary>
    AwaitingPayment = 0,
    /// <summary>In the kitchen queue.</summary>
    New = 1,
    Preparing = 2,
    Ready = 3,
    /// <summary>Served or picked up.</summary>
    Completed = 4,
    Cancelled = 5
}

public enum PaymentStatus
{
    Unpaid = 0,
    /// <summary>A qrbound payment request is open for the order.</summary>
    Pending = 1,
    Paid = 2,
    /// <summary>qrbound couldn't confirm a charge; staff check it in CardPointe.</summary>
    NeedsReview = 3
}

public enum PaymentMethod
{
    None = 0,
    /// <summary>Paid by card through qrbound (CardPointe).</summary>
    Qrbound = 1,
    Cash = 2,
    /// <summary>Development only: QrboundPos:Payments:Simulate.</summary>
    Simulated = 3
}

public enum OrderSource
{
    /// <summary>The customer ordered on their phone after scanning a QR code.</summary>
    Customer = 0,
    /// <summary>Staff rang it up on the register.</summary>
    Register = 1
}

public class Order
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RestaurantId { get; set; }
    public Guid? TableId { get; set; }

    /// <summary>The restaurant's local date the order belongs to; Number restarts each day.</summary>
    public DateOnly BusinessDate { get; set; }

    /// <summary>The ticket number for the day (1, 2, 3...).</summary>
    public int Number { get; set; }

    /// <summary>Random token for the customer's order page: /r/{slug}/order/{token}.</summary>
    public string PublicToken { get; set; } = string.Empty;

    public OrderSource Source { get; set; }
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public PaymentMethod PaymentMethod { get; set; }

    public string? CustomerName { get; set; }

    /// <summary>
    /// The qrbound user (<c>sub</c>) the order belongs to: who placed it on their phone, or who first
    /// opened a register order's pay link. Only they can open the order page.
    /// </summary>
    public string? CustomerUserId { get; set; }

    /// <summary>The customer's qrbound email, for receipts and look-ups.</summary>
    public string? CustomerEmail { get; set; }

    public string? Notes { get; set; }

    public long SubtotalCents { get; set; }
    public decimal TaxRatePercent { get; set; }
    public long TaxCents { get; set; }

    /// <summary>Subtotal plus tax: what the payment request is for.</summary>
    public long TotalCents { get; set; }

    /// <summary>The tip the customer added when paying.</summary>
    public long TipCents { get; set; }

    /// <summary>What was charged (total plus tip), once paid.</summary>
    public long? PaidCents { get; set; }

    public string Currency { get; set; } = "USD";

    /// <summary>The open (or last) qrbound QR payment request for this order.</summary>
    public Guid? QrPaymentId { get; set; }

    /// <summary>The qrbound invoice number (CardPointe orderid), to look the charge up.</summary>
    public string? PaymentInvoiceNumber { get; set; }

    /// <summary>CardPointe retref, once paid by card.</summary>
    public string? TransactionId { get; set; }

    public string? PaymentError { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PaidAt { get; set; }
    public DateTime? SentToKitchenAt { get; set; }
    public DateTime? ReadyAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? CancelledAt { get; set; }

    /// <summary>Who rang it up, for register orders.</summary>
    public string? CreatedBy { get; set; }

    /// <summary>Optimistic concurrency: kitchen screens, the customer and the payment checker all update orders.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Restaurant? Restaurant { get; set; }
    public DiningTable? Table { get; set; }
    public List<OrderLine> Lines { get; set; } = new();
}

/// <summary>An item on an order. Name and prices are copied, so menu changes don't rewrite past orders.</summary>
public class OrderLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OrderId { get; set; }
    public Guid? MenuItemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }

    /// <summary>The item's price plus its modifiers, for one.</summary>
    public long UnitPriceCents { get; set; }

    public long LineTotalCents { get; set; }

    /// <summary>The chosen modifiers, e.g. "Large, Extra cheese".</summary>
    public string? ModifiersText { get; set; }

    public string? Notes { get; set; }
    public int SortOrder { get; set; }

    public Order? Order { get; set; }
}

public enum StaffRole
{
    /// <summary>Register and kitchen.</summary>
    Staff = 0,
    /// <summary>Also the menu, tables, restaurant settings and refunds of review cases.</summary>
    Manager = 1,
    /// <summary>Also staff accounts and new restaurants.</summary>
    Owner = 2
}

/// <summary>Someone who signs in to the POS. Passwords are hashed with ASP.NET Core's PasswordHasher.</summary>
public class StaffMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public StaffRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedSignIns { get; set; }
    public DateTime? LockedUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastSignInAt { get; set; }
}
