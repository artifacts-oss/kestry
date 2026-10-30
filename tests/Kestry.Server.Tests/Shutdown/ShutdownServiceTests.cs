using Kestry.Server.Aria2;
using Kestry.Server.Downloads;
using Kestry.Server.Settings;
using Kestry.Server.Shutdown;
using Kestry.Server.Tests.Aria2;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Kestry.Server.Tests.Shutdown;

public class ShutdownServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kestry-sd").FullName;
    private readonly FakeTimeProvider _time = new();
    private readonly FakePower _power = new();
    private readonly FakeAria2 _aria2 = new();
    private readonly SettingsService _settings;
    private ShutdownService _service;

    public ShutdownServiceTests()
    {
        _settings = TestSettings.Create(_root, _aria2.Client);
        _service = Create();
    }

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private ShutdownService Create(bool allow = true, bool container = false) => new(
        _settings,
        Options.Create(new KestryOptions { DataDir = _root, AllowedRoots = [_root], AllowShutdown = allow }),
        _power, _aria2.Client, new Aria2Runtime(), _time, NullLogger<ShutdownService>.Instance, container);

    private async Task EnableAsync() => await _settings.UpdateAsync(_settings.Current with { ShutdownWhenDone = true });

    private static DownloadDto D(string gid, string status, bool seeding = false) => new() { Gid = gid, Status = status, Seeding = seeding };

    /// <summary>Feeds "a was downloading" then "a completed", i.e. one download finished during the session.</summary>
    private void FinishOne(ShutdownService? service = null)
    {
        service ??= _service;
        service.Evaluate([D("a", "downloading")], true);
        service.Evaluate([D("a", "completed")], true);
    }

    private async Task WaitForPowerAsync()
    {
        for (var i = 0; i < 100 && _power.Calls == 0; i++) await Task.Delay(10);
    }

    [Fact]
    public async Task Starts_a_60_second_countdown_when_the_queue_empties_after_a_completion()
    {
        await EnableAsync();

        FinishOne();

        Assert.Equal(60, _service.SecondsRemaining);
        _time.Advance(TimeSpan.FromSeconds(10));
        Assert.Equal(50, _service.SecondsRemaining);
    }

    [Fact]
    public async Task Powers_off_when_the_countdown_ends_and_saves_the_session_first()
    {
        await EnableAsync();
        FinishOne();

        _time.Advance(ShutdownService.Countdown);
        _service.Evaluate([D("a", "completed")], true);
        await WaitForPowerAsync();

        Assert.Equal(1, _power.Calls);
        Assert.Equal("aria2.saveSession", _aria2.Methods[0]);
    }

    [Fact]
    public void Does_nothing_when_the_option_is_off()
    {
        FinishOne();

        Assert.Null(_service.SecondsRemaining);
    }

    [Fact]
    public async Task Does_not_arm_right_after_start_when_nothing_finished_during_the_session()
    {
        await EnableAsync();

        _service.Evaluate([D("a", "completed")], true); // already finished before we started watching
        _service.Evaluate([D("a", "completed")], true);

        Assert.Null(_service.SecondsRemaining);
    }

    [Theory]
    [InlineData("downloading")]
    [InlineData("queued")]
    [InlineData("paused")]
    public async Task Waits_while_anything_is_still_pending(string pending)
    {
        await EnableAsync();
        _service.Evaluate([D("a", "downloading"), D("b", pending)], true);

        _service.Evaluate([D("a", "completed"), D("b", pending)], true);

        Assert.Null(_service.SecondsRemaining);
    }

    [Fact]
    public async Task A_seeding_torrent_blocks_shutdown()
    {
        await EnableAsync();
        _service.Evaluate([D("a", "downloading")], true);

        _service.Evaluate([D("a", "completed", seeding: true)], true);

        Assert.Null(_service.SecondsRemaining);
    }

    [Fact]
    public async Task Adding_a_download_aborts_the_countdown()
    {
        await EnableAsync();
        FinishOne();
        Assert.NotNull(_service.SecondsRemaining);

        _service.Evaluate([D("a", "completed"), D("b", "downloading")], true);

        Assert.Null(_service.SecondsRemaining);
    }

    [Fact]
    public async Task Turning_the_option_off_aborts_the_countdown()
    {
        await EnableAsync();
        FinishOne();

        await _settings.UpdateAsync(_settings.Current with { ShutdownWhenDone = false });
        _service.Evaluate([D("a", "completed")], true);

        Assert.Null(_service.SecondsRemaining);
    }

    [Fact]
    public async Task Cancel_stops_the_countdown_and_it_does_not_restart_until_something_new_completes()
    {
        await EnableAsync();
        FinishOne();

        _service.Cancel();
        _service.Evaluate([D("a", "completed")], true);
        Assert.Null(_service.SecondsRemaining);
        _time.Advance(TimeSpan.FromMinutes(5));
        _service.Evaluate([D("a", "completed")], true);
        await Task.Delay(30);
        Assert.Equal(0, _power.Calls);

        _service.Evaluate([D("a", "completed"), D("b", "downloading")], true);
        _service.Evaluate([D("a", "completed"), D("b", "completed")], true);
        Assert.Equal(60, _service.SecondsRemaining);
    }

    [Fact]
    public async Task Does_not_arm_while_aria2_is_not_running()
    {
        await EnableAsync();
        _service.Evaluate([D("a", "downloading")], true);

        _service.Evaluate([D("a", "completed")], false);

        Assert.Null(_service.SecondsRemaining);
    }

    [Theory]
    [InlineData(false, false, "disabled")]
    [InlineData(true, true, "container")]
    public async Task Unavailable_servers_never_arm(bool allow, bool container, string reason)
    {
        var service = Create(allow, container);
        await EnableAsync();

        FinishOne(service);

        Assert.Equal(reason, service.UnavailableReason);
        Assert.False(service.Available);
        Assert.Null(service.SecondsRemaining);
    }

    [Fact]
    public async Task A_failing_power_command_is_survivable_and_not_retried_in_a_loop()
    {
        _power.Fail = new InvalidOperationException("polkit says no");
        await EnableAsync();
        FinishOne();
        _time.Advance(ShutdownService.Countdown);

        _service.Evaluate([D("a", "completed")], true);
        await WaitForPowerAsync();
        await Task.Delay(50);
        _service.Evaluate([D("a", "completed")], true);

        Assert.Equal(1, _power.Calls);
        Assert.Null(_service.SecondsRemaining);
    }
}
