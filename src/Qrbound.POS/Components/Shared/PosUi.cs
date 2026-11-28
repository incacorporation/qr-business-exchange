using Qrbound.POS.Data;

namespace Qrbound.POS.Components.Shared;

/// <summary>Wording and colours for order states, shared by the customer and staff pages.</summary>
public static class PosUi
{
    public static string StatusLabel(OrderStatus s) => s switch
    {
        OrderStatus.AwaitingPayment => "Awaiting payment",
        OrderStatus.New => "In the queue",
        OrderStatus.Preparing => "Preparing",
        OrderStatus.Ready => "Ready",
        OrderStatus.Completed => "Completed",
        OrderStatus.Cancelled => "Cancelled",
        _ => s.ToString()
    };

    public static string StatusBadge(OrderStatus s) => s switch
    {
        OrderStatus.Ready or OrderStatus.Completed => "badge badge-ok",
        OrderStatus.AwaitingPayment => "badge badge-warn",
        OrderStatus.Cancelled => "badge badge-danger",
        _ => "badge"
    };

    public static string PaymentLabel(Order o) => o.PaymentStatus switch
    {
        PaymentStatus.Paid => o.PaymentMethod switch
        {
            PaymentMethod.Cash => "Paid cash",
            PaymentMethod.Simulated => "Paid (simulated)",
            _ => "Paid card"
        },
        PaymentStatus.Pending => "Payment open",
        PaymentStatus.NeedsReview => "Check payment",
        _ => "Unpaid"
    };

    public static string PaymentBadge(PaymentStatus s) => s switch
    {
        PaymentStatus.Paid => "badge badge-ok",
        PaymentStatus.NeedsReview => "badge badge-danger",
        PaymentStatus.Pending => "badge badge-warn",
        _ => "badge"
    };

    /// <summary>"Table 4" or "Takeaway".</summary>
    public static string Where(Order o) => o.Table?.Name ?? "Takeaway";

    public static string Ago(DateTime utc, DateTime nowUtc)
    {
        var span = nowUtc - utc;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes} min";
        return $"{(int)span.TotalHours} h {span.Minutes} min";
    }
}
