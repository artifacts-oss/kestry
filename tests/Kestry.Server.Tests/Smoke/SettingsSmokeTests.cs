using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kestry.Server.Aria2;
using Kestry.Server.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kestry.Server.Tests.Smoke;

public class SettingsSmokeTests(KestryFactory factory) : IClassFixture<KestryFactory>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        var runtime = factory.Services.GetRequiredService<Aria2Runtime>();
        for (var i = 0; i < 150 && runtime.State != Aria2State.Running; i++) await Task.Delay(100);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private Aria2Client Aria2 => factory.Services.GetRequiredService<Aria2Client>();

    [Aria2Fact]
    public async Task Aria2_starts_with_seeding_disabled_and_the_saved_limits()
    {
        var options = await Aria2.GetGlobalOptionAsync();

        Assert.Equal("0", options["seed-time"]); // seeding off; seed-ratio=0 would mean "forever"
        Assert.Equal("3", options["max-concurrent-downloads"]);
    }

    [Aria2Fact]
    public async Task Saving_settings_is_applied_to_the_running_aria2_and_bumps_the_version()
    {
        var http = factory.CreateClient();
        var current = (await http.GetFromJsonAsync<AppSettings>("/api/settings"))!;
        var before = (await http.GetFromJsonAsync<JsonElement>("/api/downloads")).GetProperty("settingsVersion").GetInt32();
        var newDir = Path.Combine(factory.DownloadDir, "changed");

        var response = await http.PutAsJsonAsync("/api/settings", current with
        {
            MaxConcurrentDownloads = 6, SpeedLimitEnabled = true, DownloadLimitKBps = 1000, UploadLimitKBps = 200,
            DownloadDir = newDir, SeedingEnabled = true, SeedRatio = 2,
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var options = await Aria2.GetGlobalOptionAsync();
        Assert.Equal("6", options["max-concurrent-downloads"]);
        Assert.Equal("1024000", options["max-overall-download-limit"]); // 1000K
        Assert.Equal("204800", options["max-overall-upload-limit"]);
        Assert.Equal(newDir, options["dir"]);
        Assert.Equal("2.0", options["seed-ratio"]);
        Assert.Equal("525600", options["seed-time"]);
        var after = (await http.GetFromJsonAsync<JsonElement>("/api/downloads")).GetProperty("settingsVersion").GetInt32();
        Assert.True(after > before);

        // Switching the limit off sets both to 0 but remembers the values.
        await http.PutAsJsonAsync("/api/settings", current with { DownloadDir = newDir, SpeedLimitEnabled = false });
        options = await Aria2.GetGlobalOptionAsync();
        Assert.Equal("0", options["max-overall-download-limit"]);
    }

    [Aria2Fact]
    public async Task Invalid_settings_are_rejected_with_field_errors_and_nothing_changes()
    {
        var http = factory.CreateClient();
        var current = (await http.GetFromJsonAsync<AppSettings>("/api/settings"))!;

        var response = await http.PutAsJsonAsync("/api/settings", current with { MaxConcurrentDownloads = 99, DownloadDir = "/etc" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("errors");
        Assert.True(errors.TryGetProperty("maxConcurrentDownloads", out _));
        Assert.True(errors.TryGetProperty("downloadDir", out _));
        Assert.Equal(current.MaxConcurrentDownloads, (await http.GetFromJsonAsync<AppSettings>("/api/settings"))!.MaxConcurrentDownloads);
    }

    [Aria2Fact]
    public async Task System_reports_whether_shutdown_is_available_and_cancel_is_idempotent()
    {
        var http = factory.CreateClient();

        var system = await http.GetFromJsonAsync<JsonElement>("/api/system");

        Assert.Equal(system.GetProperty("shutdownAvailable").GetBoolean(), system.GetProperty("shutdownUnavailableReason").ValueKind == JsonValueKind.Null);
        Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsync("/api/shutdown/cancel", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await http.PostAsync("/api/shutdown/cancel", null)).StatusCode);
    }
}
