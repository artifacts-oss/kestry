using Kestry.Server.Aria2;
using Kestry.Server.Shutdown;

namespace Kestry.Server.Settings;

public static class SettingsEndpoints
{
    public static void MapSettings(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/settings", (SettingsService settings) => settings.Current);

        app.MapPut("/api/settings", async (AppSettings requested, SettingsService settings, ShutdownService shutdown, CancellationToken ct) =>
        {
            try
            {
                // Turning the option on where it cannot work would silently do nothing: say so.
                if (requested.ShutdownWhenDone && !settings.Current.ShutdownWhenDone && !shutdown.Available)
                    throw new SettingsValidationException(new Dictionary<string, ErrorBody> { ["shutdownWhenDone"] = new(ErrorCodes.ShutdownUnavailable, "Shutting down is not available on this server.") });
                return Results.Ok(await settings.UpdateAsync(requested, ct));
            }
            catch (SettingsValidationException ex)
            {
                return Results.BadRequest(new { code = ErrorCodes.SettingsInvalid, error = "Invalid settings", errors = ex.Errors });
            }
        });

        app.MapGet("/api/system", (Aria2Runtime aria2, ShutdownService shutdown) => new
        {
            version = typeof(SettingsEndpoints).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            aria2Version = aria2.Version,
            shutdownAvailable = shutdown.Available,
            shutdownUnavailableReason = shutdown.UnavailableReason,
        });
    }
}
