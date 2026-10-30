using Kestry.Server.Aria2;

namespace Kestry.Server.Settings;

public static class SystemEndpoint
{
    public static void MapSystem(this IEndpointRouteBuilder app) =>
        app.MapGet("/api/system", (Aria2Runtime aria2) => new
        {
            version = typeof(SystemEndpoint).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            aria2Version = aria2.Version,
        });
}
