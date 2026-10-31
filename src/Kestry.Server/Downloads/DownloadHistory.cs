using System.Text.Json;
using Kestry.Server.Aria2;
using Microsoft.Extensions.Options;

namespace Kestry.Server.Downloads;

/// <summary>
/// Finished downloads kept across restarts. aria2's session file only holds unfinished ones, so completed and
/// failed results would vanish with the process; they are mirrored to DataDir/history.json instead.
/// </summary>
public sealed class DownloadHistory
{
    public const int MaxEntries = 1000;

    private readonly object _gate = new();
    private readonly string _file;
    private readonly ILogger<DownloadHistory> _log;
    private List<Aria2Status> _entries = [];

    public DownloadHistory(IOptions<KestryOptions> kestry, ILogger<DownloadHistory> log)
    {
        _log = log;
        _file = Path.Combine(kestry.Value.DataDir, "history.json");
        try
        {
            if (File.Exists(_file)) _entries = JsonSerializer.Deserialize<List<Aria2Status>>(File.ReadAllText(_file), Aria2Json.Options) ?? [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            log.LogWarning(ex, "Could not read {File}; starting with an empty history", _file);
        }
    }

    public IReadOnlyList<Aria2Status> Entries { get { lock (_gate) return [.. _entries]; } }

    public Aria2Status? Find(string gid) { lock (_gate) return _entries.Find(e => e.Gid == gid); }

    /// <summary>Records aria2's finished results and returns <paramref name="stopped"/> plus remembered ones aria2 no longer knows.</summary>
    public List<Aria2Status> Merge(IEnumerable<Aria2Status> active, IEnumerable<Aria2Status> waiting, IReadOnlyList<Aria2Status> stopped)
    {
        var live = active.Concat(waiting).Select(s => s.Gid).ToHashSet();
        var finished = stopped.Where(s => s.Status is "complete" or "error").ToList();
        var known = stopped.Select(s => s.Gid).ToHashSet();
        lock (_gate)
        {
            // aria2's copy wins; one that is live again (a failed download resumed from the session) leaves the history.
            var next = finished.Concat(_entries.Where(e => !known.Contains(e.Gid) && !live.Contains(e.Gid))).Take(MaxEntries).ToList();
            if (!next.Select(e => (e.Gid, e.Status)).SequenceEqual(_entries.Select(e => (e.Gid, e.Status))))
            {
                _entries = next;
                Save();
            }
            return [.. stopped, .. _entries.Where(e => !known.Contains(e.Gid))];
        }
    }

    public void Remove(string gid)
    {
        lock (_gate)
            if (_entries.RemoveAll(e => e.Gid == gid) > 0) Save();
    }

    public void Clear()
    {
        lock (_gate)
        {
            _entries = [];
            Save();
        }
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_file)!);
            var tmp = _file + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_entries, Aria2Json.Options));
            File.Move(tmp, _file, overwrite: true); // atomic, so a crash never leaves a half-written history
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.LogWarning(ex, "Could not write {File}", _file);
        }
    }
}
