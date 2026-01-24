using System.Collections.Concurrent;
using System.Net;

namespace QRBusinessExchange.API.Middleware;

/// <summary>
/// Middleware for rate limiting API requests per IP address
/// </summary>
public class RateLimitingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RateLimitingMiddleware> _logger;
    private static readonly ConcurrentDictionary<string, ClientRequestInfo> _clients = new();
    private const int MaxRequestsPerMinute = 100;
    private const int TimeWindowSeconds = 60;

    public RateLimitingMiddleware(RequestDelegate next, ILogger<RateLimitingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        
        var clientInfo = _clients.GetOrAdd(ipAddress, _ => new ClientRequestInfo());

        lock (clientInfo)
        {
            var now = DateTime.UtcNow;
            
            // Remove old requests outside the time window
            clientInfo.RequestTimestamps.RemoveAll(timestamp => 
                (now - timestamp).TotalSeconds > TimeWindowSeconds);

            // Check if rate limit is exceeded
            if (clientInfo.RequestTimestamps.Count >= MaxRequestsPerMinute)
            {
                _logger.LogWarning("Rate limit exceeded for IP: {IpAddress}", ipAddress);
                context.Response.StatusCode = (int)HttpStatusCode.TooManyRequests;
                context.Response.ContentType = "application/json";
                
                var retryAfter = TimeWindowSeconds - (int)(now - clientInfo.RequestTimestamps.First()).TotalSeconds;
                context.Response.Headers["Retry-After"] = retryAfter.ToString();
                
                return;
            }

            // Add current request timestamp
            clientInfo.RequestTimestamps.Add(now);
        }

        await _next(context);
    }

    private class ClientRequestInfo
    {
        public List<DateTime> RequestTimestamps { get; } = new();
    }

    // Cleanup old client entries periodically
    static RateLimitingMiddleware()
    {
        var timer = new System.Threading.Timer(_ =>
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-5);
            var keysToRemove = _clients
                .Where(kvp => kvp.Value.RequestTimestamps.All(t => t < cutoff))
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in keysToRemove)
            {
                _clients.TryRemove(key, out var _);
            }
        }, null, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5));
    }
}
