namespace Kestry.Server.Settings;

/// <summary>User settings persisted in settings.json. Limits are stored in KB/s.</summary>
public sealed record AppSettings
{
    public int MaxConcurrentDownloads { get; init; } = 3;
    public bool SpeedLimitEnabled { get; init; }
    public int DownloadLimitKBps { get; init; } = 5 * 1024;
    public int UploadLimitKBps { get; init; } = 1024;
    public string DownloadDir { get; init; } = KestryOptions.DefaultDownloadDir;
    public bool ShutdownWhenDone { get; init; }
    public bool SeedingEnabled { get; init; }
    public double SeedRatio { get; init; } = 1.0;
}
