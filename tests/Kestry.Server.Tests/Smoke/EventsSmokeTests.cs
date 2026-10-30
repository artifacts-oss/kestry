using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kestry.Server.Aria2;
using Microsoft.Extensions.DependencyInjection;

namespace Kestry.Server.Tests.Smoke;

public class EventsSmokeTests(KestryFactory factory) : IClassFixture<KestryFactory>
{
    private static async Task<(string EventName, JsonElement Data)> ReadEventAsync(Stream stream, CancellationToken ct)
    {
        using var reader = new StreamReader(stream);
        string? name = null, data = null, line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            if (line.StartsWith("event:")) name = line[6..].Trim();
            else if (line.StartsWith("data:")) data = line[5..].Trim();
            else if (line.Length == 0 && data is not null) return (name!, JsonDocument.Parse(data).RootElement.Clone());
        }
        throw new InvalidOperationException("The stream ended before an event arrived");
    }

    [Aria2Fact]
    public async Task Events_stream_a_snapshot_with_no_cache_headers_and_live_data()
    {
        var runtime = factory.Services.GetRequiredService<Aria2Runtime>();
        for (var i = 0; i < 150 && runtime.State != Aria2State.Running; i++) await Task.Delay(100);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var http = factory.CreateClient();

        using var response = await http.GetAsync("/api/events", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        var (name, snapshot) = await ReadEventAsync(await response.Content.ReadAsStreamAsync(cts.Token), cts.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType!.MediaType);
        Assert.Contains("no-cache", response.Headers.CacheControl!.ToString());
        Assert.Equal("snapshot", name);
        Assert.Equal("running", snapshot.GetProperty("aria2").GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Array, snapshot.GetProperty("downloads").ValueKind);
        Assert.True(snapshot.GetProperty("stats").GetProperty("freeDiskBytes").GetInt64() > 0);
        Assert.Equal(JsonValueKind.Null, snapshot.GetProperty("shutdown").ValueKind);
    }

    [Aria2Fact]
    public async Task Get_downloads_returns_the_same_snapshot_shape()
    {
        var snapshot = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/downloads");

        Assert.True(snapshot.TryGetProperty("downloads", out _));
        Assert.True(snapshot.TryGetProperty("settingsVersion", out _));
    }
}

public class SystemSmokeTests(KestryFactory factory) : IClassFixture<KestryFactory>
{
    [Aria2Fact]
    public async Task System_reports_the_aria2_version_once_running()
    {
        var runtime = factory.Services.GetRequiredService<Aria2Runtime>();
        for (var i = 0; i < 150 && runtime.State != Aria2State.Running; i++) await Task.Delay(100);

        var system = await factory.CreateClient().GetFromJsonAsync<JsonElement>("/api/system");

        Assert.Matches(@"^\d+\.\d+", system.GetProperty("aria2Version").GetString());
        Assert.Matches(@"^\d+\.\d+\.\d+$", system.GetProperty("version").GetString());
    }
}
