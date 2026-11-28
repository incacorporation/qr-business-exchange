using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;
using Qrbound.POS.Data;

namespace Qrbound.POS.Services;

/// <summary>The customer-facing links put in QR codes: a table's menu, and an order's pay page.</summary>
public sealed class PosLinks(NavigationManager nav, IOptions<QrboundPosOptions> options)
{
    private string Base => (string.IsNullOrWhiteSpace(options.Value.PublicBaseUrl) ? nav.BaseUri : options.Value.PublicBaseUrl).TrimEnd('/') + "/";

    /// <summary>The menu for a table: what the table's printed QR code opens.</summary>
    public string Table(Restaurant r, DiningTable t) => $"{Base}r/{r.Slug}/t/{t.Code}";

    /// <summary>The menu for takeaway (counter or window QR code).</summary>
    public string Takeaway(Restaurant r) => $"{Base}r/{r.Slug}";

    /// <summary>An order's page, where the customer pays it.</summary>
    public string Order(Restaurant r, Order o) => $"{Base}r/{r.Slug}/order/{o.PublicToken}";
}
