using System.IdentityModel.Tokens.Jwt;
using QRBusinessExchange.Infrastructure.Services;

namespace QRBusinessExchange.API.Middleware;

/// <summary>
/// Middleware for validating JWT tokens and attaching user information to the request context
/// </summary>
public class JwtMiddleware
{
    private readonly RequestDelegate _next;

    public JwtMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, JwtService jwtService)
    {
        var token = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();

        if (!string.IsNullOrEmpty(token))
        {
            var userId = jwtService.ValidateToken(token);
            if (userId.HasValue)
            {
                // Attach user ID to context for use in controllers
                context.Items["UserId"] = userId.Value;
            }
        }

        await _next(context);
    }
}
