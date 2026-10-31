using System.Text.Json;
using Kestry.Server.Aria2;
using Kestry.Server.Downloads;
using Microsoft.Extensions.Logging.Abstractions;

namespace Kestry.Server.Tests.Downloads;

public class DownloadHistoryTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kestry-hist").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private DownloadHistory Open() => new(TestSettings.Options(_root), NullLogger<DownloadHistory>.Instance);

    private static Aria2Status S(string gid, string status) => new() { Gid = gid, Status = status, TotalLength = 10, CompletedLength = 10 };

    private static string[] Gids(IEnumerable<Aria2Status> list) => list.Select(s => s.Gid).ToArray();

    [Fact]
    public void Finished_downloads_survive_a_restart_of_aria2_and_of_the_app()
    {
        Open().Merge([], [], [S("done", "complete"), S("bad", "error"), S("gone", "removed")]);

        // New process: aria2 forgot its results, the history did not.
        var merged = Open().Merge([S("new", "active")], [], []);

        Assert.Equal(["done", "bad"], Gids(merged));
        Assert.Equal(10, merged[0].TotalLength);
    }

    [Fact]
    public void Aria2_copy_comes_first_and_is_not_duplicated()
    {
        var h = Open();
        h.Merge([], [], [S("old", "complete")]);

        Assert.Equal(["new", "old"], Gids(h.Merge([], [], [S("new", "complete"), S("old", "complete")])));
        Assert.Equal(["new", "old"], Gids(h.Entries));
    }

    [Fact]
    public void A_failed_download_resumed_by_aria2_leaves_the_history()
    {
        var h = Open();
        h.Merge([], [], [S("g", "error")]);

        Assert.Empty(h.Merge([S("g", "active")], [], []));
        Assert.Empty(h.Entries);
    }

    [Fact]
    public void Remove_and_clear_are_persisted()
    {
        var h = Open();
        h.Merge([], [], [S("a", "complete"), S("b", "complete")]);
        h.Remove("a");
        Assert.Equal(["b"], Gids(Open().Entries));

        h.Clear();
        Assert.Empty(Open().Entries);
    }

    [Fact]
    public void Keeps_at_most_MaxEntries()
    {
        var h = Open();
        h.Merge([], [], Enumerable.Range(0, DownloadHistory.MaxEntries + 5).Select(i => S($"g{i}", "complete")).ToList());

        Assert.Equal(DownloadHistory.MaxEntries, h.Entries.Count);
        Assert.Equal("g0", h.Entries[0].Gid);
    }

    [Fact]
    public void A_corrupt_file_starts_an_empty_history()
    {
        Directory.CreateDirectory(Path.Combine(_root, "data"));
        File.WriteAllText(Path.Combine(_root, "data", "history.json"), "{not json");

        Assert.Empty(Open().Entries);
    }
}
