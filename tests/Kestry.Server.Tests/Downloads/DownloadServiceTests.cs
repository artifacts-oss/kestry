using Kestry.Server.Aria2;
using Kestry.Server.Downloads;
using Kestry.Server.Settings;
using Kestry.Server.Tests.Aria2;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Kestry.Server.Tests.Downloads;

public class DownloadServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kestry-dl").FullName;
    private readonly FakeAria2 _aria2 = new();
    private readonly DownloadService _service;

    public DownloadServiceTests()
    {
        var options = TestSettings.Options(_root);
        _service = new DownloadService(_aria2.Client, TestSettings.Create(_root, _aria2.Client), options, NullLogger<DownloadService>.Instance);
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Downloads => Path.Combine(_root, "downloads");

    private string StatusJson(string status, string dir, params string[] files) =>
        $$"""{"gid":"g1","status":"{{status}}","dir":{{System.Text.Json.JsonSerializer.Serialize(dir)}},"files":[{{string.Join(",", files.Select(f => $$"""{"index":"1","path":{{System.Text.Json.JsonSerializer.Serialize(f)}},"uris":[{"uri":"http://x/a.bin"}]}"""))}}]}""";

    private string File_(string relative, string content = "x")
    {
        var path = Path.Combine(Downloads, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    [Fact]
    public async Task AddUri_targets_the_download_folder_and_subfolder_and_honours_paused()
    {
        await _service.AddUriAsync("http://x/a.bin", "movies", paused: true);

        var (method, p) = _aria2.Calls.Single();
        Assert.Equal("aria2.addUri", method);
        Assert.Equal(Path.Combine(Downloads, "movies"), p[2].GetProperty("dir").GetString());
        Assert.Equal("true", p[2].GetProperty("pause").GetString());
    }

    [Theory]
    [InlineData("../escape")]
    [InlineData("/etc")]
    public async Task AddUri_rejects_subfolders_that_escape(string sub)
    {
        await Assert.ThrowsAsync<InvalidDownloadException>(() => _service.AddUriAsync("http://x/a.bin", sub));
        Assert.Empty(_aria2.Calls);
    }

    [Fact]
    public async Task Remove_active_download_cancels_it_and_deletes_partial_files()
    {
        var partial = File_("movies/a.bin");
        var control = File_("movies/a.bin.aria2");
        _aria2.On("aria2.tellStatus", StatusJson("active", Downloads, partial));

        await _service.RemoveAsync("g1", deleteFiles: true);

        Assert.Equal(["aria2.tellStatus", "aria2.forceRemove", "aria2.removeDownloadResult"], _aria2.Methods);
        Assert.False(File.Exists(partial));
        Assert.False(File.Exists(control));
        Assert.False(Directory.Exists(Path.Combine(Downloads, "movies")), "empty parent folder is pruned");
        Assert.True(Directory.Exists(Downloads), "the download folder itself is kept");
    }

    [Fact]
    public async Task Remove_finished_download_skips_forceRemove_and_keeps_files_by_default()
    {
        var file = File_("a.bin");
        _aria2.On("aria2.tellStatus", StatusJson("complete", Downloads, file));

        await _service.RemoveAsync("g1", deleteFiles: false);

        Assert.Equal(["aria2.tellStatus", "aria2.removeDownloadResult"], _aria2.Methods);
        Assert.True(File.Exists(file));
    }

    [Fact]
    public async Task Remove_never_deletes_files_outside_the_allowed_roots()
    {
        var outside = Path.Combine(_root, "outside.bin");
        File.WriteAllText(outside, "keep");
        _aria2.On("aria2.tellStatus", StatusJson("complete", Downloads, outside));

        await _service.RemoveAsync("g1", deleteFiles: true);

        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task Remove_does_not_follow_dot_dot_out_of_the_roots()
    {
        var outside = Path.Combine(_root, "outside.bin");
        File.WriteAllText(outside, "keep");
        _aria2.On("aria2.tellStatus", StatusJson("complete", Downloads, Path.Combine(Downloads, "..", "outside.bin")));

        await _service.RemoveAsync("g1", deleteFiles: true);

        Assert.True(File.Exists(outside));
    }

    [Fact]
    public async Task Retry_re_adds_the_original_uri_in_the_same_folder_and_drops_the_old_entry()
    {
        _aria2.On("aria2.tellStatus", StatusJson("error", Downloads, "/x/a.bin")).On("aria2.addUri", "\"g2\"");

        var gid = await _service.RetryAsync("g1");

        Assert.Equal("g2", gid);
        var add = _aria2.Calls.Single(c => c.Method == "aria2.addUri").Params;
        Assert.Equal("http://x/a.bin", add[1][0].GetString());
        Assert.Equal(Downloads, add[2].GetProperty("dir").GetString());
        Assert.Equal("aria2.removeDownloadResult", _aria2.Methods[^1]);
    }

    [Fact]
    public async Task Retry_falls_back_to_a_magnet_for_torrents()
    {
        _aria2.On("aria2.tellStatus", """{"gid":"g1","status":"error","infoHash":"abc123","dir":"/d","files":[{"index":"1","path":"/d/t"}]}""");

        await _service.RetryAsync("g1");

        Assert.Equal("magnet:?xt=urn:btih:abc123", _aria2.Calls.Single(c => c.Method == "aria2.addUri").Params[1][0].GetString());
    }

    [Fact]
    public async Task Retry_without_any_source_fails()
    {
        _aria2.On("aria2.tellStatus", """{"gid":"g1","status":"error","files":[]}""");

        await Assert.ThrowsAsync<InvalidDownloadException>(() => _service.RetryAsync("g1"));
    }
}
