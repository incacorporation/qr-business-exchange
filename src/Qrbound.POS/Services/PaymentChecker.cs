using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Qrbound.POS.Data;

namespace Qrbound.POS.Services;

/// <summary>
/// Checks open payment requests with qrbound every few seconds, so an order is marked paid even
/// when the customer closes their phone straight after paying, or pays on Fiserv's hosted page.
/// </summary>
internal sealed class PaymentChecker(IServiceScopeFactory scopes, IOptions<QrboundPosOptions> options, TimeProvider clock, ILogger<PaymentChecker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.Payments.Simulate) return;

        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.Payments.CheckIntervalSeconds));
        using var timer = new PeriodicTimer(interval, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CheckOnceAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Checking open payments with qrbound failed");
            }
        }
    }

    private async Task CheckOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<PosDbContext>>().CreateDbContextAsync(ct);
        await using var _ = db;

        // Requests live at most 24 hours; look a little further back.
        var since = clock.GetUtcNow().UtcDateTime.AddHours(-26);
        var pending = await db.Orders.AsNoTracking()
            .Where(o => o.PaymentStatus == PaymentStatus.Pending && o.QrPaymentId != null && o.Status != OrderStatus.Cancelled && o.CreatedAt > since)
            .OrderBy(o => o.CreatedAt)
            .Select(o => o.Id)
            .Take(100)
            .ToListAsync(ct);

        if (pending.Count == 0) return;

        var checkout = scope.ServiceProvider.GetRequiredService<CheckoutService>();
        foreach (var id in pending)
        {
            try
            {
                await checkout.RefreshPaymentAsync(id, ct: ct);
            }
            catch (PosException ex)
            {
                logger.LogWarning("Payment check of order {OrderId}: {Reason}", id, ex.Message);
            }
        }
    }
}
