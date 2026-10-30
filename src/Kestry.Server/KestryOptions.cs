namespace Kestry.Server;

public sealed class KestryOptions
{
    public string DataDir { get; set; } = "./data";

    /// <summary>Roots the download folder (and file deletion) may live under. Empty = the default folder only.</summary>
    public string[] AllowedRoots { get; set; } = [];

    public bool AllowShutdown { get; set; } = true;

    /// <summary>Extra origins (e.g. the tunnel's domain) accepted on mutating requests.</summary>
    public string[] AllowedOrigins { get; set; } = [];

    public static string DefaultDownloadDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Kestry");

    public string[] EffectiveRoots => AllowedRoots.Length > 0 ? AllowedRoots : [DefaultDownloadDir];
}

public sealed class Aria2Options
{
    /// <summary>Explicit aria2c path; null = PATH, then DataDir/aria2, then download (Windows only).</summary>
    public string? Path { get; set; }

    public int Port { get; set; } = 6800;
    public int BtPort { get; set; } = 6881;
    public string[] ExtraArgs { get; set; } = [];
}
