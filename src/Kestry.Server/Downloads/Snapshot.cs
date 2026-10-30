namespace Kestry.Server.Downloads;

public sealed record DownloadDto
{
    public string Gid { get; init; } = "";
    public string Name { get; init; } = "";
    public string Path { get; init; } = "";
    /// <summary>downloading | queued | paused | completed | failed</summary>
    public string Status { get; init; } = "";
    /// <summary>http | https | ftp | torrent | magnet</summary>
    public string Source { get; init; } = "http";
    /// <summary>file | video | archive | music | image</summary>
    public string FileType { get; init; } = "file";
    public long Size { get; init; }
    public long Downloaded { get; init; }
    public long Speed { get; init; }
    public long UploadSpeed { get; init; }
    public long? Eta { get; init; }
    public int Connections { get; init; }
    public int Seeders { get; init; }
    public bool FetchingMetadata { get; init; }
    /// <summary>Fully downloaded torrent that is still uploading.</summary>
    public bool Seeding { get; init; }
    public string? Error { get; init; }
}

public sealed record StatsDto(long DownloadSpeed, long UploadSpeed, long? FreeDiskBytes);

public sealed record Aria2Dto(string State, string? ReasonCode, string? Reason);

public sealed record Snapshot(
    IReadOnlyList<DownloadDto> Downloads,
    StatsDto Stats,
    Aria2Dto Aria2,
    int? Shutdown,
    int SettingsVersion);
