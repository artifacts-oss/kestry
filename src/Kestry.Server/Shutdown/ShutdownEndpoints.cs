namespace Kestry.Server.Shutdown;

public static class ShutdownEndpoints
{
    public static void MapShutdown(this IEndpointRouteBuilder app) =>
        app.MapPost("/api/shutdown/cancel", (ShutdownService shutdown) =>
        {
            shutdown.Cancel();
            return Results.NoContent();
        });
}
