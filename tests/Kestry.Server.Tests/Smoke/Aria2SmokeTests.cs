using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Kestry.Server.Aria2;
using Kestry.Server.Downloads;
using Microsoft.Extensions.DependencyInjection;

namespace Kestry.Server.Tests.Smoke;

/// <summary>Runs the whole stack against a real aria2c process and a local HTTP server.</summary>
public class Aria2SmokeTests : IClassFixture<KestryFactory>, IAsyncLifetime
{
    private readonly KestryFactory _factory;
    private LocalFileServer _files = null!;

    public Aria2SmokeTests(KestryFactory factory) => _factory = factory;

    public async Task InitializeAsync()
    {
        _files = await LocalFileServer.StartAsync();
        var runtime = _factory.Services.GetRequiredService<Aria2Runtime>();
        await WaitAsync(() => runtime.State == Aria2State.Running, "aria2 to start");
    }

    public async Task DisposeAsync() => await _files.DisposeAsync();

    private static async Task WaitAsync(Func<bool> condition, string what)
    {
        for (var i = 0; i < 150; i++)
        {
            if (condition()) return;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Timed out waiting for {what}");
    }

    private static async Task WaitAsync(Func<Task<bool>> condition, string what)
    {
        for (var i = 0; i < 150; i++)
        {
            if (await condition()) return;
            await Task.Delay(100);
        }
        throw new TimeoutException($"Timed out waiting for {what}");
    }

    private Aria2Client Aria2 => _factory.Services.GetRequiredService<Aria2Client>();
    private DownloadService Downloads => _factory.Services.GetRequiredService<DownloadService>();

    [Aria2Fact]
    public async Task Downloads_a_local_http_file_to_completion()
    {
        var gid = await Downloads.AddUriAsync($"{_files.BaseUrl}/small.bin", subfolder: "http");

        await WaitAsync(async () => (await Aria2.TellStatusAsync(gid)).Status == "complete", "the download to complete");

        var saved = await File.ReadAllBytesAsync(Path.Combine(_factory.DownloadDir, "http", "small.bin"));
        Assert.Equal(LocalFileServer.Small, saved);
    }

    [Aria2Fact]
    public async Task Pause_resume_and_cancel_through_the_REST_api()
    {
        var http = _factory.CreateClient();
        var gid = await Downloads.AddUriAsync($"{_files.BaseUrl}/slow.bin", subfolder: "slow");
        await WaitAsync(async () => (await Aria2.TellStatusAsync(gid)).CompletedLength > 0, "the download to start");

        Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsync($"/api/downloads/{gid}/pause", null)).StatusCode);
        await WaitAsync(async () => (await Aria2.TellStatusAsync(gid)).Status == "paused", "pause");

        Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsync($"/api/downloads/{gid}/resume", null)).StatusCode);
        await WaitAsync(async () => (await Aria2.TellStatusAsync(gid)).Status == "active", "resume");

        var partial = Path.Combine(_factory.DownloadDir, "slow", "slow.bin");
        Assert.True(File.Exists(partial));
        Assert.Equal(HttpStatusCode.NoContent, (await http.DeleteAsync($"/api/downloads/{gid}?deleteFiles=true")).StatusCode);
        Assert.False(File.Exists(partial));
        Assert.False(File.Exists(partial + ".aria2"));
        var gone = await Assert.ThrowsAsync<Aria2Exception>(() => Aria2.TellStatusAsync(gid));
        Assert.Equal(1, gone.Code);
    }

    [Aria2Fact]
    public async Task Snapshot_lists_a_live_download()
    {
        var gid = await Downloads.AddUriAsync($"{_files.BaseUrl}/slow.bin", subfolder: "snapshot");
        await WaitAsync(async () => (await Aria2.TellStatusAsync(gid)).CompletedLength > 0, "the download to start");

        var snapshot = await _factory.CreateClient().GetFromJsonAsync<System.Text.Json.JsonElement>("/api/downloads");

        var row = snapshot.GetProperty("downloads").EnumerateArray().Single(d => d.GetProperty("gid").GetString() == gid);
        Assert.Equal("downloading", row.GetProperty("status").GetString());
        Assert.Equal("slow.bin", row.GetProperty("name").GetString());
        Assert.Equal("http", row.GetProperty("source").GetString());
        Assert.True(row.GetProperty("downloaded").GetInt64() > 0);
        await _factory.CreateClient().DeleteAsync($"/api/downloads/{gid}?deleteFiles=true");
    }

    [Aria2Fact]
    public async Task Pasted_links_are_added_and_duplicates_of_existing_downloads_are_skipped()
    {
        var http = _factory.CreateClient();
        var text = $"{_files.BaseUrl}/small.bin?a=1\n{_files.BaseUrl}/small.bin?a=1\nnot-a-link\n# comment";

        var first = await (await http.PostAsJsonAsync("/api/downloads/links", new { text, subfolder = "pasted", paused = true }))
            .Content.ReadFromJsonAsync<DownloadEndpoints.LinksResponse>();

        Assert.Equal((1, 1, 1), (first!.Added, first.Duplicates, first.Invalid));
        Assert.Equal(["added", "duplicate", "invalid"], first.Links.Select(l => l.State));
        var added = first.Links[0];
        Assert.Equal("paused", (await Aria2.TellStatusAsync(added.Gid!)).Status);

        // The same link again is now a duplicate of an existing download.
        var second = await (await http.PostAsJsonAsync("/api/downloads/links", new { text = $"{_files.BaseUrl}/small.bin?a=1" }))
            .Content.ReadFromJsonAsync<DownloadEndpoints.LinksResponse>();
        Assert.Equal(["duplicate"], second!.Links.Select(l => l.State));

        var preview = await (await http.PostAsJsonAsync("/api/downloads/links/preview", new { text = $"{_files.BaseUrl}/small.bin?a=1\n{_files.BaseUrl}/new.bin" }))
            .Content.ReadFromJsonAsync<DownloadEndpoints.LinksResponse>();
        Assert.Equal((1, 1, 0), (preview!.Added, preview.Duplicates, preview.Invalid));
    }

    [Aria2Fact]
    public async Task Links_requests_are_validated()
    {
        var http = _factory.CreateClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/downloads/links", new { text = "  " })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/downloads/links", new { text = new string('a', DownloadEndpoints.MaxLinksTextLength + 1) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await http.PostAsJsonAsync("/api/downloads/links", new { text = "http://a.com/x", subfolder = "../escape" })).StatusCode);
    }

    [Aria2Fact]
    public async Task Unknown_gid_is_a_404()
    {
        var response = await _factory.CreateClient().PostAsync("/api/downloads/0000000000000000/pause", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Aria2Fact]
    public async Task Uploads_torrents_and_rejects_garbage()
    {
        var form = new MultipartFormDataContent
        {
            { new ByteArrayContent(Torrent("ok.bin")), "files", "ok.torrent" },
            { new ByteArrayContent("not a torrent"u8.ToArray()), "files", "bad.torrent" },
            { new ByteArrayContent([1]), "files", "wrong.txt" },
        };

        var response = await _factory.CreateClient().PostAsync("/api/downloads/torrents?paused=true", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var results = (await response.Content.ReadFromJsonAsync<DownloadEndpoints.TorrentResult[]>())!;
        Assert.Equal(["added", "invalid", "invalid"], results.Select(r => r.Status));
        var status = await Aria2.TellStatusAsync(results[0].Gid!);
        Assert.Equal("paused", status.Status);
    }

    [Aria2Fact]
    public async Task Retry_re_adds_a_failed_download()
    {
        var gid = await Downloads.AddUriAsync($"{_files.BaseUrl}/missing.bin");
        await WaitAsync(async () => (await Aria2.TellStatusAsync(gid)).Status == "error", "the download to fail");

        var response = await _factory.CreateClient().PostAsync($"/api/downloads/{gid}/retry", null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var newGid = (await response.Content.ReadFromJsonAsync<Dictionary<string, string>>())!["gid"];
        Assert.NotEqual(gid, newGid);
        Assert.Equal(1, (await Assert.ThrowsAsync<Aria2Exception>(() => Aria2.TellStatusAsync(gid))).Code);
    }

    /// <summary>Builds a minimal single-file v1 .torrent (bencoded) so aria2 accepts it.</summary>
    private static byte[] Torrent(string name)
    {
        var piece = SHA1.HashData(new byte[16384]);
        var head = Encoding.ASCII.GetBytes($"d4:infod6:lengthi16384e4:name{name.Length}:{name}12:piece lengthi16384e6:pieces{piece.Length}:");
        return [.. head, .. piece, .. "ee"u8.ToArray()];
    }
}
