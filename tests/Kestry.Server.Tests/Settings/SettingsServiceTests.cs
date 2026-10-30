using System.Text.Json;
using Kestry.Server.Aria2;
using Kestry.Server.Settings;
using Kestry.Server.Tests.Aria2;

namespace Kestry.Server.Tests.Settings;

public class SettingsServiceTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kestry-set").FullName;
    private readonly FakeAria2 _aria2 = new();
    private readonly Aria2Runtime _runtime = new() { State = Aria2State.Running };
    private SettingsService Create() => TestSettings.Create(_root, _aria2.Client, _runtime);
    private string Downloads => Path.Combine(_root, "downloads");

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void Defaults_to_the_first_allowed_root()
    {
        var s = Create().Current;

        Assert.Equal(Downloads, s.DownloadDir);
        Assert.Equal(3, s.MaxConcurrentDownloads);
        Assert.False(s.SpeedLimitEnabled);
        Assert.False(s.ShutdownWhenDone);
        Assert.Equal(5120, s.DownloadLimitKBps);
        Assert.Equal(1024, s.UploadLimitKBps);
    }

    [Fact]
    public async Task Saves_to_settings_json_and_reloads_it_on_the_next_start()
    {
        var service = Create();

        await service.UpdateAsync(service.Current with { MaxConcurrentDownloads = 7, ShutdownWhenDone = true });

        Assert.Equal(7, Create().Current.MaxConcurrentDownloads);
        Assert.True(Create().Current.ShutdownWhenDone);
        Assert.Equal(1, service.Version);
    }

    [Fact]
    public async Task Version_changes_on_every_save()
    {
        var service = Create();

        await service.UpdateAsync(service.Current);
        await service.UpdateAsync(service.Current);

        Assert.Equal(2, service.Version);
    }

    [Fact]
    public void A_corrupted_settings_file_falls_back_to_defaults()
    {
        Directory.CreateDirectory(Path.Combine(_root, "data"));
        File.WriteAllText(Path.Combine(_root, "data", "settings.json"), "{ not json");

        Assert.Equal(3, Create().Current.MaxConcurrentDownloads);
    }

    [Fact]
    public async Task Applies_changes_to_aria2_without_restarting_it()
    {
        var service = Create();

        await service.UpdateAsync(service.Current with
        {
            MaxConcurrentDownloads = 5, SpeedLimitEnabled = true, DownloadLimitKBps = 2048, UploadLimitKBps = 512,
            DownloadDir = Path.Combine(Downloads, "new"), SeedingEnabled = true, SeedRatio = 1.5,
        });

        var (method, p) = _aria2.Calls.Single();
        Assert.Equal("aria2.changeGlobalOption", method);
        var o = p[1];
        Assert.Equal("5", o.GetProperty("max-concurrent-downloads").GetString());
        Assert.Equal("2048K", o.GetProperty("max-overall-download-limit").GetString());
        Assert.Equal("512K", o.GetProperty("max-overall-upload-limit").GetString());
        Assert.Equal(Path.Combine(Downloads, "new"), o.GetProperty("dir").GetString());
        Assert.Equal("1.5", o.GetProperty("seed-ratio").GetString());
        Assert.Equal("525600", o.GetProperty("seed-time").GetString());
        Assert.True(Directory.Exists(Path.Combine(Downloads, "new")), "the new folder is created");
    }

    [Fact]
    public async Task Turning_the_speed_limit_off_sets_both_limits_to_zero_but_keeps_the_values()
    {
        var service = Create();
        await service.UpdateAsync(service.Current with { SpeedLimitEnabled = true, DownloadLimitKBps = 100 });
        _aria2.Calls.Clear();

        var saved = await service.UpdateAsync(service.Current with { SpeedLimitEnabled = false });

        var o = _aria2.Calls.Single().Params[1];
        Assert.Equal("0K", o.GetProperty("max-overall-download-limit").GetString());
        Assert.Equal("0K", o.GetProperty("max-overall-upload-limit").GetString());
        Assert.Equal(100, saved.DownloadLimitKBps);
    }

    [Fact]
    public void Seeding_off_disables_seeding_with_seed_time_zero_because_ratio_zero_would_seed_forever()
    {
        var o = SettingsService.ToAria2Options(new AppSettings { SeedingEnabled = false, SeedRatio = 2 });

        Assert.Equal("0", o["seed-time"]);
        Assert.NotEqual("0.0", o["seed-ratio"]);
    }

    [Fact]
    public async Task Saves_without_calling_aria2_when_it_is_not_running()
    {
        _runtime.State = Aria2State.Down;
        var service = Create();

        await service.UpdateAsync(service.Current with { MaxConcurrentDownloads = 4 });

        Assert.Empty(_aria2.Calls);
        Assert.Equal(4, service.Current.MaxConcurrentDownloads);
    }

    [Fact]
    public async Task Normalizes_the_download_folder()
    {
        var service = Create();

        var saved = await service.UpdateAsync(service.Current with { DownloadDir = Downloads + "/a/../b/" });

        Assert.Equal(Path.Combine(Downloads, "b"), saved.DownloadDir);
    }

    [Theory]
    [InlineData(0, "maxConcurrentDownloads")]
    [InlineData(21, "maxConcurrentDownloads")]
    public async Task Rejects_out_of_range_concurrency(int value, string field)
    {
        var service = Create();

        var ex = await Assert.ThrowsAsync<SettingsValidationException>(() => service.UpdateAsync(service.Current with { MaxConcurrentDownloads = value }));

        Assert.Contains(field, ex.Errors.Keys);
        Assert.Equal(ErrorCodes.MaxConcurrentRange, ex.Errors[field].Code);
        Assert.Equal(3, service.Current.MaxConcurrentDownloads);
        Assert.Equal(0, service.Version);
    }

    [Theory]
    [InlineData("relative/path")]
    [InlineData("")]
    [InlineData("/etc")]
    public async Task Rejects_download_folders_outside_the_allowed_roots(string dir)
    {
        var service = Create();

        var ex = await Assert.ThrowsAsync<SettingsValidationException>(() => service.UpdateAsync(service.Current with { DownloadDir = dir }));

        Assert.Contains("downloadDir", ex.Errors.Keys);
        Assert.Equal(dir == "/etc" ? ErrorCodes.DirOutsideRoots : ErrorCodes.DirNotAbsolute, ex.Errors["downloadDir"].Code);
    }

    [Fact]
    public async Task Rejects_dot_dot_escapes_and_sibling_prefixes()
    {
        var service = Create();

        foreach (var dir in new[] { Path.Combine(Downloads, "..", "other"), Downloads + "-evil" })
            await Assert.ThrowsAsync<SettingsValidationException>(() => service.UpdateAsync(service.Current with { DownloadDir = dir }));
    }

    [Fact]
    public async Task Rejects_a_download_folder_that_cannot_be_created()
    {
        var service = Create();
        Directory.CreateDirectory(Downloads);
        File.WriteAllText(Path.Combine(Downloads, "file"), "x");

        var ex = await Assert.ThrowsAsync<SettingsValidationException>(() => service.UpdateAsync(service.Current with { DownloadDir = Path.Combine(Downloads, "file", "sub") }));

        Assert.Equal(ErrorCodes.DirNotWritable, ex.Errors["downloadDir"].Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Rejects_non_positive_limits(int kbps)
    {
        var service = Create();

        var ex = await Assert.ThrowsAsync<SettingsValidationException>(() =>
            service.UpdateAsync(service.Current with { DownloadLimitKBps = kbps, UploadLimitKBps = kbps }));

        Assert.Contains("downloadLimitKBps", ex.Errors.Keys);
        Assert.Contains("uploadLimitKBps", ex.Errors.Keys);
    }

    [Fact]
    public async Task Seed_ratio_only_matters_when_seeding_is_enabled()
    {
        var service = Create();

        await service.UpdateAsync(service.Current with { SeedingEnabled = false, SeedRatio = 0 });
        await Assert.ThrowsAsync<SettingsValidationException>(() => service.UpdateAsync(service.Current with { SeedingEnabled = true, SeedRatio = 0 }));
    }

    [Fact]
    public async Task The_file_on_disk_is_valid_json_after_saving()
    {
        var service = Create();
        await service.UpdateAsync(service.Current);

        var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(_root, "data", "settings.json")));

        Assert.Equal(3, json.RootElement.GetProperty("maxConcurrentDownloads").GetInt32());
        Assert.False(File.Exists(Path.Combine(_root, "data", "settings.json.tmp")));
    }
}
