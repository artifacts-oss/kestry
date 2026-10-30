namespace Kestry.Server.Downloads;

/// <summary>Ticks every second and publishes a snapshot, but only while somebody is listening.</summary>
public sealed class DownloadMonitor(SnapshotBuilder builder, SnapshotHub hub, TimeProvider time, ILogger<DownloadMonitor> log) : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval, time);
        while (await WaitAsync(timer, stoppingToken))
        {
            if (hub.SubscriberCount == 0) continue;
            try
            {
                hub.Publish(await builder.BuildAsync(stoppingToken));
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
