using ClaudeTaskScheduler.Services;

namespace ClaudeTaskScheduler.Api;

/// <summary>
/// Requires a valid X-Api-Key header on every /api call except the
/// unauthenticated /api/ping probe. The static mobile dashboard is served
/// separately and is not gated (it prompts the visitor for the key and sends
/// it on each fetch).
/// </summary>
public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppSettings settings)
    {
        var path = context.Request.Path;

        if (!path.StartsWithSegments("/api") || path == "/api/ping")
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var provided) ||
            provided.Count == 0 ||
            provided[0] != settings.ApiKey)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Missing or invalid X-Api-Key header.");
            return;
        }

        await _next(context);
    }
}
