using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qrbound.Payments.Models;
using Qrbound.POS.Data;

namespace Qrbound.POS.Services;

/// <summary>
/// Takes payment for orders through Qrbound.Payments. Each order gets a qrbound QR payment request
/// for its total, created on the server; the customer pays it with <c>QrboundPayLink</c> on their
/// phone (or by scanning the register's QR code). An order counts as paid only when qrbound itself
/// says so, never because the browser said it was.
/// </summary>
public sealed class CheckoutService(
    IDbContextFactory<PosDbContext> dbFactory,
    QrboundBusinessClient qrbound,
    OrderService orders,
    PosEvents events,
    IOptions<QrboundPosOptions> options,
    TimeProvider clock,
    ILogger<CheckoutService> logger)
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> OrderLocks = new();

    /// <summary>Development only: payments are simulated instead of going to qrbound.</summary>
    public bool Simulated => options.Value.Payments.Simulate;

    /// <summary>
    /// The qrbound payment request to pay this order with: the open one if there is one for the
    /// right amount, otherwise a new one. Null when the order needs no payment (already paid) or
    /// payments are simulated.
    /// </summary>
    public async Task<Guid?> StartPaymentAsync(Guid orderId, CancellationToken ct = default)
    {
        var gate = OrderLocks.GetOrAdd(orderId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            await using var db = await dbFactory.CreateDbContextAsync(ct);
            var order = await db.Orders.Include(o => o.Restaurant).Include(o => o.Table).FirstOrDefaultAsync(o => o.Id == orderId, ct)
                ?? throw new PosException("Order not found.");

            if (order.Status == OrderStatus.Cancelled) throw new PosException($"Order {order.Number} was cancelled.");
            if (order.PaymentStatus == PaymentStatus.Paid || Simulated) return null;
            if (order.PaymentStatus == PaymentStatus.NeedsReview)
            {
                throw new PosException("We couldn't confirm an earlier payment for this order. Please ask a member of staff before paying again.");
            }

            var businessId = order.Restaurant!.QrboundBusinessId
                ?? throw new PosException($"{order.Restaurant.Name} can't take card payments yet. Please pay a member of staff.");

            if (order.QrPaymentId is { } existing)
            {
                var current = await qrbound.GetPaymentAsync(businessId, existing, ct);
                if (Apply(order, current))
                {
                    await db.SaveChangesAsync(ct);
                    events.Publish(order.RestaurantId, order.Id);
                }

                if (order.PaymentStatus is PaymentStatus.Paid) return null;
                if (current.Status is QrPaymentStates.Open or QrPaymentStates.Processing && current.AmountCents == order.TotalCents)
                {
                    return existing;
                }

                if (current.Status is QrPaymentStates.Open)
                {
                    await qrbound.CancelPaymentAsync(businessId, existing, ct); // wrong amount: replace it
                }
                else if (order.PaymentStatus is PaymentStatus.NeedsReview)
                {
                    throw new PosException("We couldn't confirm an earlier payment for this order. Please ask a member of staff before paying again.");
                }
            }

            var description = order.Table is { } t
                ? $"{order.Restaurant.Name} order {order.Number}, {t.Name}"
                : $"{order.Restaurant.Name} order {order.Number}";
            var created = await qrbound.CreatePaymentAsync(businessId, order.TotalCents, description, ct);

            order.QrPaymentId = created.Id;
            order.PaymentInvoiceNumber = created.InvoiceNumber;
            order.PaymentStatus = PaymentStatus.Pending;
            order.PaymentError = null;
            await db.SaveChangesAsync(ct);
            events.Publish(order.RestaurantId, order.Id);
            logger.LogInformation("Order {OrderId} ({Number}) payment request {PaymentId} for {Amount} cents", order.Id, order.Number, created.Id, order.TotalCents);
            return created.Id;
        }
        finally
        {
            gate.Release();
        }
    }

    /// <summary>
    /// Asks qrbound where the order's payment stands and records it. Call it when the customer's
    /// page says it was paid; <see cref="PaymentChecker"/> also calls it in the background.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <param name="askGateway">Also have qrbound look the bill up with CardPointe (slower).</param>
    /// <param name="ct">Cancels the check.</param>
    public async Task<Order?> RefreshPaymentAsync(Guid orderId, bool askGateway = false, CancellationToken ct = default)
    {
        await using var db = await dbFactory.CreateDbContextAsync(ct);
        var order = await db.Orders.Include(o => o.Restaurant).FirstOrDefaultAsync(o => o.Id == orderId, ct);
        if (order?.QrPaymentId is not { } paymentId || order.Restaurant?.QrboundBusinessId is not { } businessId) return order;
        if (order.PaymentStatus == PaymentStatus.Paid) return order;

        var details = askGateway
            ? await qrbound.CheckWithGatewayAsync(businessId, paymentId, ct)
            : await qrbound.GetPaymentAsync(businessId, paymentId, ct);

        if (Apply(order, details))
        {
            try
            {
                await db.SaveChangesAsync(ct);
                events.Publish(order.RestaurantId, order.Id);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Someone else updated the order at the same moment; the next check records it.
            }
        }

        return order;
    }

    /// <summary>Cancels an order, and its open qrbound payment request so nobody can still pay it.</summary>
    public async Task CancelOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        var order = await RefreshPaymentAsync(orderId, ct: ct) ?? throw new PosException("Order not found.");
        if (order.PaymentStatus == PaymentStatus.Pending && order.QrPaymentId is { } paymentId && order.Restaurant?.QrboundBusinessId is { } businessId)
        {
            try
            {
                await qrbound.CancelPaymentAsync(businessId, paymentId, ct);
            }
            catch (PosException ex)
            {
                // Paid or processing in the meantime: don't cancel an order someone is paying.
                await RefreshPaymentAsync(orderId, ct: ct);
                throw new PosException($"The payment request couldn't be cancelled ({ex.Message}). Check the order before cancelling.");
            }
        }

        await orders.CancelAsync(orderId);
    }

    /// <summary>Development only: marks the order paid without charging anything.</summary>
    public async Task SimulatePaymentAsync(Guid orderId, long tipCents)
    {
        if (!Simulated) throw new PosException("Simulated payments are off.");
        if (tipCents < 0) throw new PosException("The tip can't be negative.");

        await orders.UpdateAsync(orderId, o =>
        {
            if (o.Status == OrderStatus.Cancelled) throw new PosException($"Order {o.Number} was cancelled.");
            if (o.PaymentStatus == PaymentStatus.Paid) return;
            OrderService.MarkPaid(o, PaymentMethod.Simulated, o.TotalCents + tipCents, tipCents, "SIMULATED", clock.GetUtcNow().UtcDateTime);
        });
    }

    /// <summary>Copies qrbound's state of the payment request onto the order; true when something changed.</summary>
    private bool Apply(Order order, QrPaymentDetails p)
    {
        if (order.PaymentStatus == PaymentStatus.Paid) return false;

        switch (p.Status)
        {
            case QrPaymentStates.Paid:
                OrderService.MarkPaid(order, PaymentMethod.Qrbound, p.PaidAmountCents ?? p.AmountCents, p.TipCents, p.TransactionId, p.PaidAt ?? clock.GetUtcNow().UtcDateTime);
                if (p.AmountCents != order.TotalCents)
                {
                    order.PaymentError = $"Paid {Money.Format(p.AmountCents, order.Currency)} for a {Money.Format(order.TotalCents, order.Currency)} order.";
                    logger.LogWarning("Order {OrderId} paid {Paid} cents but totals {Total}", order.Id, p.AmountCents, order.TotalCents);
                }

                return true;

            case QrPaymentStates.NeedsReview:
                return Set(order, PaymentStatus.NeedsReview, p.LastError ?? "The card payment couldn't be confirmed. Look the invoice up in CardPointe.");

            case QrPaymentStates.Cancelled or QrPaymentStates.Locked or QrPaymentStates.Expired:
                return Set(order, PaymentStatus.Unpaid, p.Status == QrPaymentStates.Locked ? "Too many declined cards; a new payment request is needed." : null);

            default: // Open, Processing
                return Set(order, PaymentStatus.Pending, p.FailedAttempts > 0 ? p.LastError : null);
        }
    }

    private static bool Set(Order order, PaymentStatus status, string? error)
    {
        if (order.PaymentStatus == status && order.PaymentError == error) return false;
        order.PaymentStatus = status;
        order.PaymentError = error is { Length: > 300 } ? error[..300] : error;
        return true;
    }
}
