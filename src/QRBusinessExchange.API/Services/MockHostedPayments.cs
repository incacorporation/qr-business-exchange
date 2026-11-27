using System.Collections.Concurrent;

namespace QRBusinessExchange.API.Services;

/// <summary>
/// Development only: payments made on the API's stand-in for Fiserv's Hosted Payment Page, which the
/// mock gateway's order inquiry reports. Lives in memory and empties when the API restarts.
/// </summary>
public static class MockHostedPayments
{
    /// <summary>Where the stand-in page is served, relative to the API's base address.</summary>
    public const string PagePath = "mock-hpp/pay";

    private static readonly ConcurrentDictionary<string, List<CardPointeTransaction>> Orders = new(StringComparer.OrdinalIgnoreCase);

    public static CardPointeTransaction Record(string orderId, long amountCents, string? name)
    {
        var transaction = new CardPointeTransaction
        {
            RetRef = $"MOCKHPP{DateTime.UtcNow:yyMMddHHmmssfff}",
            AuthCode = "MOCK01",
            AmountCents = amountCents,
            RespStat = "A",
            SetlStat = "Queued for Capture",
            Name = name
        };

        var list = Orders.GetOrAdd(orderId, _ => new List<CardPointeTransaction>());
        lock (list)
        {
            list.Add(transaction);
        }

        return transaction;
    }

    public static IReadOnlyList<CardPointeTransaction> For(string orderId)
    {
        if (!Orders.TryGetValue(orderId, out var list))
        {
            return [];
        }

        lock (list)
        {
            return list.ToList();
        }
    }
}
