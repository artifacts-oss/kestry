using Kestry.Server.Aria2;
using Kestry.Server.Settings;
using Microsoft.Extensions.Options;

namespace Kestry.Server.Downloads;

public sealed class InvalidDownloadException(string message) : Exception(message);

public sealed class DownloadService(
    Aria2Client aria2,
    SettingsService settings,
    IOptions<KestryOptions> kestry,
    ILogger<DownloadService> log)
{
    private Dictionary<string, string> AddOptions(string? subfolder, bool paused)
    {
        var baseDir = settings.Current.DownloadDir;
        var dir = PathGuard.ResolveSubfolder(baseDir, subfolder)
            ?? throw new InvalidDownloadException("The subfolder must be relative and stay inside the download folder.");
        var options = new Dictionary<string, string> { ["dir"] = dir };
        if (paused) options["pause"] = "true";
        return options;
    }

    public Task<string> AddUriAsync(string uri, string? subfolder = null, bool paused = false, CancellationToken ct = default) =>
        aria2.AddUriAsync([uri], AddOptions(subfolder, paused), ct);

    public Task<string> AddTorrentAsync(byte[] torrent, string? subfolder = null, bool paused = false, CancellationToken ct = default) =>
        aria2.AddTorrentAsync(torrent, AddOptions(subfolder, paused), ct);

    public Task PauseAsync(string gid, CancellationToken ct = default) => aria2.PauseAsync(gid, ct);
    public Task ResumeAsync(string gid, CancellationToken ct = default) => aria2.UnpauseAsync(gid, ct);
    public Task PauseAllAsync(CancellationToken ct = default) => aria2.PauseAllAsync(ct);
    public Task ResumeAllAsync(CancellationToken ct = default) => aria2.UnpauseAllAsync(ct);
    public Task PurgeAsync(CancellationToken ct = default) => aria2.PurgeDownloadResultAsync(ct);

    /// <summary>Re-adds a stopped download from its original URI (or its magnet, for torrents) and drops the old entry.</summary>
    public async Task<string> RetryAsync(string gid, CancellationToken ct = default)
    {
        var status = await aria2.TellStatusAsync(gid, ct);
        var uris = status.Files.SelectMany(f => f.Uris).Select(u => u.Uri).Distinct().ToArray();
        if (uris.Length == 0 && status.InfoHash is { Length: > 0 } hash) uris = [$"magnet:?xt=urn:btih:{hash}"];
        if (uris.Length == 0) throw new InvalidDownloadException("This download has no source to retry from.");

        var options = status.Dir is null ? null : new Dictionary<string, string> { ["dir"] = status.Dir };
        var newGid = await aria2.AddUriAsync(uris, options, ct);
        await aria2.RemoveDownloadResultAsync(gid, ct);
        return newGid;
    }

    /// <summary>Stops/removes a download; with <paramref name="deleteFiles"/> also deletes its (partial) files.</summary>
    public async Task RemoveAsync(string gid, bool deleteFiles, CancellationToken ct = default)
    {
        var status = await aria2.TellStatusAsync(gid, ct);
        if (status.Status is "active" or "waiting" or "paused") await aria2.ForceRemoveAsync(gid, ct);
        await aria2.RemoveDownloadResultAsync(gid, ct);
        if (deleteFiles) DeleteFiles(status);
    }

    private void DeleteFiles(Aria2Status status)
    {
        var roots = kestry.Value.EffectiveRoots;
        foreach (var file in status.Files.Select(f => f.Path).Where(p => p.Length > 0))
        {
            if (!PathGuard.IsUnderAny(file, roots))
            {
                log.LogWarning("Refusing to delete {Path}: outside the allowed roots", file);
                continue;
            }
            // aria2 keeps a .aria2 control file next to unfinished downloads.
            TryDelete(file);
            TryDelete(file + ".aria2");
            PruneEmptyParents(file, status.Dir, roots);
        }
    }

    private void TryDelete(string path)
    {
        try { File.Delete(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { log.LogWarning(ex, "Could not delete {Path}", path); }
    }

    private static void PruneEmptyParents(string file, string? downloadDir, IEnumerable<string> roots)
    {
        var stop = downloadDir is null ? null : Path.TrimEndingDirectorySeparator(Path.GetFullPath(downloadDir));
        for (var dir = Path.GetDirectoryName(Path.GetFullPath(file)); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (dir == stop || roots.Any(r => Path.TrimEndingDirectorySeparator(Path.GetFullPath(r)) == dir)) return;
            if (!Directory.Exists(dir) || Directory.EnumerateFileSystemEntries(dir).Any()) return;
            try { Directory.Delete(dir); } catch (IOException) { return; }
        }
    }
}
