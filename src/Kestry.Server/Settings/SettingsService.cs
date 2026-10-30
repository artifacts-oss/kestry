using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Kestry.Server.Settings;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly string _file;

    public SettingsService(IOptions<KestryOptions> options)
    {
        var o = options.Value;
        Directory.CreateDirectory(o.DataDir);
        _file = Path.Combine(o.DataDir, "settings.json");
        // A configured root becomes the default folder so Docker (/downloads) needs no extra setting.
        var defaults = new AppSettings { DownloadDir = o.EffectiveRoots[0] };
        Current = File.Exists(_file)
            ? JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(_file), Json) ?? defaults
            : defaults;
    }

    public AppSettings Current { get; private set; }

    /// <summary>Bumped on every save so other browser tabs know to reload the settings.</summary>
    public int Version { get; private set; }
}
