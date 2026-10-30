using Kestry.Server.Aria2;
using Kestry.Server.Settings;
using Kestry.Server.Shutdown;

namespace Kestry.Server.Downloads;

/// <summary>Ticks every second and publishes a snapshot while somebody is listening or a shutdown is armed.</summary>
public sealed class DownloadMonitor(
    SnapshotBuilder builder,
    SnapshotHub hub,
    ShutdownService shutdown,
    SettingsService settings,
    Aria2Runtime runtime,
    TimeProvider time,
    ILogger<DownloadMonitor> log) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        while (await WaitAsync(timer, stoppingToken))
        {
            var watchingForShutdown = settings.Current.ShutdownWhenDone && shutdown.Available;
            if (hub.SubscriberCount == 0 && !watchingForShutdown) continue;
            try
            {
                var snapshot = await builder.BuildAsync(stoppingToken);
                shutdown.Evaluate(snapshot.Downloads, runtime.State == Aria2State.Running);
                hub.Publish(snapshot);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                log.LogWarning(ex, "Could not build a snapshot");
            }
        }
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try { return await timer.WaitForNextTickAsync(ct); }
        catch (OperationCanceledException) { return false; }
    }
}
