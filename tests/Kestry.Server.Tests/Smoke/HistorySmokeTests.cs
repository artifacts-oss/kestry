using Kestry.Server.Aria2;
using Kestry.Server.Downloads;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kestry.Server.Tests.Smoke;

/// <summary>Regression: a completed download must still be listed after Kestry (and aria2) restart, e.g. a new image.</summary>
public class HistorySmokeTests : IDisposable
{
    private readonly string _data = Directory.CreateTempSubdirectory("kestry-hist-it").FullName;

    public void Dispose() => Directory.Delete(_data, recursive: true);

    private sealed class SharedDataFactory(string dataDir) : KestryFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration((_, cfg) => cfg.AddInMemoryCollection(new Dictionary<string, string?> { ["Kestry:DataDir"] = dataDir }));
        }
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

    private static Task WaitRunningAsync(KestryFactory f) =>
        WaitAsync(() => Task.FromResult(f.Services.GetRequiredService<Aria2Runtime>().State == Aria2State.Running), "aria2 to start");

    [Aria2Fact]
    public async Task Completed_download_is_still_listed_after_a_restart()
    {
        await using var files = await LocalFileServer.StartAsync();
        string gid;
        using (var first = new SharedDataFactory(_data))
        {
            await WaitRunningAsync(first);
            gid = await first.Services.GetRequiredService<DownloadService>().AddUriAsync($"{files.BaseUrl}/small.bin");
            var aria2 = first.Services.GetRequiredService<Aria2Client>();
            await WaitAsync(async () => (await aria2.TellStatusAsync(gid)).Status == "complete", "the download to complete");
            // No browser connected: the graceful stop alone must record it.
        }

        using var second = new SharedDataFactory(_data);
        await WaitRunningAsync(second);
        var snapshot = await second.Services.GetRequiredService<SnapshotBuilder>().BuildAsync(default);

        var item = Assert.Single(snapshot.Downloads, d => d.Gid == gid);
        Assert.Equal("completed", item.Status);
        Assert.Equal("small.bin", item.Name);
    }
}
