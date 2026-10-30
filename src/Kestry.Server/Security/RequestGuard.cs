using System.Net;
using Microsoft.Extensions.Options;

namespace Kestry.Server.Security;

/// <summary>
/// Kestry has no login, so /api only answers requests that really come from its own page:
/// the Host must be loopback/an IP/an allowed domain (stops DNS rebinding) and, on mutating requests,
/// a browser's Origin must be the page's own origin or an allowed one (stops cross-site forgery).
/// </summary>
public sealed class RequestGuard(RequestDelegate next, IOptions<KestryOptions> options)
{
    private readonly HashSet<string> _allowedOrigins = options.Value.AllowedOrigins
        .Select(o => o.TrimEnd('/').ToLowerInvariant()).ToHashSet();

    private readonly HashSet<string> _allowedHosts = options.Value.AllowedOrigins
        .Select(o => Uri.TryCreate(o, UriKind.Absolute, out var u) ? u.Authority.ToLowerInvariant() : null)
        .Where(h => h is not null).Select(h => h!).ToHashSet();

    public Task InvokeAsync(HttpContext ctx)
    {
        if (!ctx.Request.Path.StartsWithSegments("/api")) return next(ctx);

        if (!HostAllowed(ctx.Request.Host)) return Reject(ctx, "Host not allowed. Add its origin to Kestry:AllowedOrigins.");
        if (!HttpMethods.IsGet(ctx.Request.Method) && !HttpMethods.IsHead(ctx.Request.Method) && !HttpMethods.IsOptions(ctx.Request.Method)
            && ctx.Request.Headers.Origin.ToString() is { Length: > 0 } origin && !OriginAllowed(origin, ctx.Request))
            return Reject(ctx, "Origin not allowed. Add it to Kestry:AllowedOrigins.");

        return next(ctx);
    }

    private bool HostAllowed(HostString host)
    {
        var name = host.Host;
        return name.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || name.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || IPAddress.TryParse(name.Trim('[', ']'), out _)
            || _allowedHosts.Contains(host.ToString().ToLowerInvariant())
            || _allowedHosts.Contains(name.ToLowerInvariant());
    }

    private bool OriginAllowed(string origin, HttpRequest request)
    {
        var normalized = origin.TrimEnd('/').ToLowerInvariant();
        return normalized == $"{request.Scheme}://{request.Host}".ToLowerInvariant() || _allowedOrigins.Contains(normalized);
    }

    private static Task Reject(HttpContext ctx, string message)
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return ctx.Response.WriteAsJsonAsync(new { error = message });
    }
}
