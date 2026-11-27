using System.Text.Json;

namespace QRBusinessExchange.API.Services;

/// <summary>
/// Finds the invoice number in a CardPointe Hosted Payment Page webhook. Nothing else in the webhook
/// is used: the payment is looked up with CardPointe by that invoice number (the order ID).
/// </summary>
public static class HostedPageWebhookParser
{
    private static readonly string[] InvoiceKeys = ["invoice", "invoiceNumber", "orderid", "orderId"];

    /// <summary>The invoice number from a JSON or form-encoded body, or null.</summary>
    public static string? FindInvoice(string body, string? contentType)
    {
        if (string.IsNullOrWhiteSpace(body) || body.Length > 64 * 1024)
        {
            return null;
        }

        var trimmed = body.TrimStart();
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            try
            {
                using var json = JsonDocument.Parse(body, new JsonDocumentOptions { MaxDepth = 16 });
                return Clean(FindInJson(json.RootElement));
            }
            catch (JsonException)
            {
                return null;
            }
        }

        if (contentType?.Contains("x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase) == true || body.Contains('='))
        {
            var form = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(body);
            foreach (var key in InvoiceKeys)
            {
                var match = form.FirstOrDefault(kv => string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase));
                if (match.Key != null)
                {
                    return Clean(match.Value.ToString());
                }
            }
        }

        return null;
    }

    private static string? FindInJson(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    if (InvoiceKeys.Any(k => string.Equals(k, property.Name, StringComparison.OrdinalIgnoreCase)) &&
                        property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                    {
                        return property.Value.ToString();
                    }
                }

                foreach (var property in element.EnumerateObject())
                {
                    if (FindInJson(property.Value) is { } nested)
                    {
                        return nested;
                    }
                }

                return null;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (FindInJson(item) is { } nested)
                    {
                        return nested;
                    }
                }

                return null;

            default:
                return null;
        }
    }

    /// <summary>Invoice numbers are short (at most 19 characters, e.g. QR-1A2B3C4D5E6F).</summary>
    private static string? Clean(string? value)
    {
        var v = value?.Trim();
        return string.IsNullOrEmpty(v) || v.Length > 19 ? null : v;
    }
}
