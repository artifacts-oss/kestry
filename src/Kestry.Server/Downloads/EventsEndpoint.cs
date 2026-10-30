using System.Net.ServerSentEvents;

namespace Kestry.Server.Downloads;

public static class EventsEndpoint
{
    public static void MapEvents(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/downloads", (SnapshotBuilder builder, CancellationToken ct) => builder.BuildAsync(ct));

        app.MapGet("/api/events", (HttpContext ctx, SnapshotHub hub, SnapshotBuilder builder, CancellationToken ct) =>
        {
            // Proxies and tunnels must not hold the stream back.
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers["X-Accel-Buffering"] = "no";
            return TypedResults.ServerSentEvents(Stream(hub, builder, ct), eventType: "snapshot");
        });
    }

    private static async IAsyncEnumerable<Snapshot> Stream(SnapshotHub hub, SnapshotBuilder builder, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        // The first event must not wait for the monitor's next tick.
        yield return hub.Latest ?? await builder.BuildAsync(ct);
        await foreach (var s in hub.SubscribeAsync(ct)) yield return s;
    }
}
