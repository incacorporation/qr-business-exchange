using System.Globalization;
using System.Security.Cryptography;

namespace Qrbound.POS.Services;

public static class Money
{
    /// <summary>"$12.50" for USD; "12.50 EUR" otherwise.</summary>
    public static string Format(long cents, string currency = "USD")
    {
        var amount = (cents / 100m).ToString("N2", CultureInfo.InvariantCulture);
        return currency switch
        {
            "USD" or "CAD" or "AUD" => (cents < 0 ? "-$" + amount.TrimStart('-') : "$" + amount),
            "GBP" => "£" + amount,
            "EUR" => "€" + amount,
            _ => amount + " " + currency
        };
    }

    /// <summary>Tax on a subtotal, rounded to the cent (half away from zero).</summary>
    public static long Tax(long subtotalCents, decimal ratePercent) =>
        (long)Math.Round(subtotalCents * ratePercent / 100m, MidpointRounding.AwayFromZero);

    /// <summary>Parses "12.50" (or "12,50") into cents; false for anything else.</summary>
    public static bool TryParse(string? text, out long cents)
    {
        cents = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var cleaned = text.Trim().TrimStart('$', '£', '€').Replace(',', '.');
        if (!decimal.TryParse(cleaned, NumberStyles.Number, CultureInfo.InvariantCulture, out var value) || value < 0 || decimal.Round(value, 2) != value)
        {
            return false;
        }

        cents = (long)(value * 100m);
        return true;
    }

    public static string ToInput(long cents) => (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}

internal static class Tokens
{
    private const string Alphabet = "abcdefghijkmnpqrstuvwxyz23456789"; // no 0/o, 1/l

    /// <summary>A random, URL-safe, unguessable token (length × 5 bits).</summary>
    public static string New(int length = 22) => RandomNumberGenerator.GetString(Alphabet, length);
}

/// <summary>Something changed on an order; screens showing it reload.</summary>
public sealed record OrderChanged(Guid RestaurantId, Guid OrderId);

/// <summary>
/// Tells open kitchen screens, registers and customer order pages that an order changed, so they
/// update without polling. In-process: with several app servers, put a backplane behind this.
/// </summary>
public sealed class PosEvents
{
    public event Action<OrderChanged>? OrderChanged;

    public void Publish(Guid restaurantId, Guid orderId) => OrderChanged?.Invoke(new(restaurantId, orderId));
}

/// <summary>A refused POS operation; Message is safe to show.</summary>
public sealed class PosException(string message) : Exception(message);

/// <summary>One line of a cart, as the customer or register built it. Prices are looked up again on the server.</summary>
public sealed class CartLine
{
    public Guid Key { get; set; } = Guid.NewGuid();
    public Guid MenuItemId { get; set; }
    public int Quantity { get; set; } = 1;
    public List<Guid> ModifierIds { get; set; } = new();
    public string? Notes { get; set; }

    // Display copies, for the cart before the server prices it.
    public string Name { get; set; } = string.Empty;
    public string? ModifiersText { get; set; }
    public long UnitPriceCents { get; set; }
}
