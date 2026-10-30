using System.Text.Json;
using Kestry.Server.Aria2;
using Microsoft.Extensions.Options;

namespace Kestry.Server.Settings;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _file;
    private readonly KestryOptions _options;
    private readonly Aria2Client _aria2;
    private readonly Aria2Runtime _runtime;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public SettingsService(IOptions<KestryOptions> options, Aria2Client aria2, Aria2Runtime runtime)
    {
        _options = options.Value;
        _aria2 = aria2;
        _runtime = runtime;
        Directory.CreateDirectory(_options.DataDir);
        _file = Path.Combine(_options.DataDir, "settings.json");
        // A configured root becomes the default folder so Docker (/downloads) needs no extra setting.
        var defaults = new AppSettings { DownloadDir = _options.EffectiveRoots[0] };
        Current = File.Exists(_file) ? Load(defaults) : defaults;
    }

    public AppSettings Current { get; private set; }

    /// <summary>Bumped on every save so other browser tabs know to reload the settings.</summary>
    public int Version { get; private set; }

    private AppSettings Load(AppSettings defaults)
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_file), Json) ?? defaults;
        }
        catch (JsonException)
        {
            return defaults; // a corrupted file must not keep the app from starting
        }
    }

    /// <summary>The aria2 options that mirror a settings object, as sent to aria2.changeGlobalOption.</summary>
    public static Dictionary<string, string> ToAria2Options(AppSettings s)
    {
        var limited = s.SpeedLimitEnabled;
        return new()
        {
            ["max-concurrent-downloads"] = s.MaxConcurrentDownloads.ToString(),
            ["max-overall-download-limit"] = $"{(limited ? s.DownloadLimitKBps : 0)}K",
            ["max-overall-upload-limit"] = $"{(limited ? s.UploadLimitKBps : 0)}K",
            ["dir"] = s.DownloadDir,
            // seed-ratio=0 would mean "seed forever" in aria2; seed-time=0 is what disables seeding.
            ["seed-ratio"] = (s.SeedingEnabled ? s.SeedRatio : 1.0).ToString("0.0#", System.Globalization.CultureInfo.InvariantCulture),
            ["seed-time"] = s.SeedingEnabled ? "525600" : "0",
        };
    }

    public async Task<AppSettings> UpdateAsync(AppSettings requested, CancellationToken ct = default)
    {
        var errors = SettingsValidator.Validate(requested, _options);
        if (errors.Count == 0 && SettingsValidator.CheckWritable(requested.DownloadDir) is { } problem) errors["downloadDir"] = problem;
        if (errors.Count > 0) throw new SettingsValidationException(errors);

        var normalized = requested with { DownloadDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(requested.DownloadDir)) };
        await _lock.WaitAsync(ct);
        try
        {
            var tmp = _file + ".tmp";
            await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(normalized, Json), ct);
            File.Move(tmp, _file, overwrite: true);
            Current = normalized;
            Version++;
        }
        finally
        {
            _lock.Release();
        }

        // Hot-apply. If aria2 is not up, it will pick the values up from BuildArgs when it starts.
        if (_runtime.State == Aria2State.Running)
        {
            try { await _aria2.ChangeGlobalOptionAsync(ToAria2Options(normalized), ct); }
            catch (Exception ex) when (ex is HttpRequestException or Aria2Exception) { /* saved; applied on the next start */ }
        }
        return normalized;
    }
}
