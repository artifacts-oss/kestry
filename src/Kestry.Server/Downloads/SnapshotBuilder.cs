using System.Text.Json;
using Kestry.Server.Aria2;
using Kestry.Server.Settings;
using Kestry.Server.Shutdown;

namespace Kestry.Server.Downloads;

/// <summary>Queries aria2 once (a single system.multicall) and assembles the snapshot sent to the UI.</summary>
public sealed class SnapshotBuilder(Aria2Client aria2, Aria2Runtime runtime, DownloadHistory history, SettingsService settings, ShutdownService shutdown, ILogger<SnapshotBuilder> log)
{
    private const int PageSize = 1000;

    public async Task<Snapshot> BuildAsync(CancellationToken ct)
    {
        var downloads = new List<DownloadDto>();
        var stat = new Aria2GlobalStat();

        if (runtime.State == Aria2State.Running)
        {
            try
            {
                var r = await aria2.MulticallAsync(
                [
                    Aria2Client.TellActive(),
                    Aria2Client.TellWaiting(0, PageSize),
                    Aria2Client.TellStopped(-1, PageSize), // negative offset = newest first
                    Aria2Client.GlobalStat(),
                ], ct);
                List<Aria2Status> Read(JsonElement e) => e.Deserialize<List<Aria2Status>>(Aria2Json.Options) ?? [];
                var (active, waiting) = (Read(r[0]), Read(r[1]));
                downloads = SnapshotMapper.Map(active, waiting, history.Merge(active, waiting, Read(r[2])));
                stat = r[3].Deserialize<Aria2GlobalStat>(Aria2Json.Options) ?? stat;
            }
            catch (Exception ex) when (ex is HttpRequestException or Aria2Exception or TaskCanceledException && !ct.IsCancellationRequested)
            {
                // aria2 is restarting or busy; the next tick will recover. Report an empty list meanwhile.
                log.LogWarning(ex, "Could not read downloads from aria2");
            }
        }

        return new Snapshot(
            downloads,
            new StatsDto(stat.DownloadSpeed, stat.UploadSpeed, FreeDiskBytes(settings.Current.DownloadDir)),
            new Aria2Dto(runtime.State.ToString().ToLowerInvariant(), runtime.ReasonCode, runtime.Reason),
            shutdown.SecondsRemaining,
            settings.Version);
    }

    /// <summary>Free space of the volume that holds <paramref name="dir"/> (or its nearest existing parent).</summary>
    public static long? FreeDiskBytes(string dir)
    {
        try
        {
            var path = Path.GetFullPath(dir);
            while (!Directory.Exists(path) && Path.GetDirectoryName(path) is { } parent) path = parent;
            return new DriveInfo(path).AvailableFreeSpace;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}
