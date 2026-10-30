using Kestry.Server.Aria2;
using Kestry.Server.Downloads;
using Kestry.Server.Settings;
using Kestry.Server.Tests.Aria2;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Kestry.Server.Tests.Downloads;

public class MonitorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kestry-mon").FullName;
    private readonly FakeTimeProvider _time = new();
    private readonly SnapshotHub _hub = new();
    private readonly Aria2Runtime _runtime = new();
    private readonly DownloadMonitor _monitor;

    public MonitorTests()
    {
        var options = Options.Create(new KestryOptions { DataDir = _root, AllowedRoots = [_root] });
        var builder = new SnapshotBuilder(new FakeAria2().Client, _runtime, new SettingsService(options), NullLogger<SnapshotBuilder>.Instance);
        _monitor = new DownloadMonitor(builder, _hub, _time, NullLogger<DownloadMonitor>.Instance);
    }

    public void Dispose()
    {
        _monitor.Dispose();
        Directory.Delete(_root, recursive: true);
    }

    private async Task TickAsync(Func<bool>? done = null)
    {
        _time.Advance(DownloadMonitor.Interval);
        for (var i = 0; i < 40 && done?.Invoke() != true; i++) await Task.Delay(25); // let the background loop run
        if (done is null) await Task.Delay(50);
    }

    [Fact]
    public async Task Publishes_nothing_while_nobody_listens()
    {
        await _monitor.StartAsync(default);

        await TickAsync();

        Assert.Null(_hub.Latest);
        await _monitor.StopAsync(default);
    }

    [Fact]
    public async Task Publishes_a_snapshot_every_tick_once_a_client_is_connected()
    {
        await _monitor.StartAsync(default);
        using var cts = new CancellationTokenSource();
        var received = new List<Snapshot>();
        var reader = Task.Run(async () =>
        {
            try { await foreach (var s in _hub.SubscribeAsync(cts.Token)) received.Add(s); }
            catch (OperationCanceledException) { }
        });
        await Task.Delay(50);

        await TickAsync(() => received.Count >= 1);
        await TickAsync(() => received.Count >= 2);
        cts.Cancel();
        await reader;

        Assert.Equal(2, received.Count);
        Assert.Equal("down", received[0].Aria2.State);
        await _monitor.StopAsync(default);
    }

    [Fact]
    public async Task A_subscriber_gets_the_latest_snapshot_immediately_and_unsubscribes_on_cancel()
    {
        _hub.Publish(new Snapshot([], new StatsDto(0, 0, null), new Aria2Dto("running", null, null), null, 0));
        using var cts = new CancellationTokenSource();

        await foreach (var first in _hub.SubscribeAsync(cts.Token))
        {
            Assert.Equal("running", first.Aria2.State);
            Assert.Equal(1, _hub.SubscriberCount);
            break;
        }

        Assert.Equal(0, _hub.SubscriberCount);
    }
}

public class FreeDiskTests
{
    [Fact]
    public void Reports_free_space_even_when_the_folder_does_not_exist_yet()
    {
        var missing = Path.Combine(Path.GetTempPath(), "kestry-not-created", "deep");

        Assert.True(SnapshotBuilder.FreeDiskBytes(missing) > 0);
    }
}
