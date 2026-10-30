using Kestry.Server.Aria2;

namespace Kestry.Server.Downloads;

/// <summary>Turns aria2 statuses into the lean DTOs the UI renders. Pure, so it is easy to test.</summary>
public static class SnapshotMapper
{
    private const string MetadataPrefix = "[METADATA]";

    private static readonly Dictionary<string, string> FileTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = "video", [".mkv"] = "video", [".avi"] = "video", [".mov"] = "video", [".webm"] = "video", [".wmv"] = "video", [".m4v"] = "video",
        [".zip"] = "archive", [".rar"] = "archive", [".7z"] = "archive", [".tar"] = "archive", [".gz"] = "archive", [".bz2"] = "archive",
        [".xz"] = "archive", [".iso"] = "archive",
        [".mp3"] = "music", [".flac"] = "music", [".wav"] = "music", [".ogg"] = "music", [".m4a"] = "music", [".aac"] = "music",
        [".jpg"] = "image", [".jpeg"] = "image", [".png"] = "image", [".gif"] = "image", [".webp"] = "image", [".bmp"] = "image", [".svg"] = "image",
    };

    /// <summary>Active first, then waiting/paused, then finished ones (newest first). Input order within each group is kept.</summary>
    public static List<DownloadDto> Map(IEnumerable<Aria2Status> active, IEnumerable<Aria2Status> waiting, IEnumerable<Aria2Status> stopped)
    {
        var all = active.Concat(waiting).Concat(stopped).ToList();

        // A magnet (or .torrent URL) first downloads metadata under its own GID and hands over to a new one
        // through followedBy. Show only the successor, and remember how the user added it.
        var sourceOfSuccessor = new Dictionary<string, string>();
        foreach (var s in all.Where(s => s.FollowedBy is { Length: > 0 }))
        {
            var first = s.Files.SelectMany(f => f.Uris).Select(u => u.Uri).FirstOrDefault() ?? "";
            foreach (var next in s.FollowedBy!) sourceOfSuccessor[next] = first.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) ? "magnet" : "torrent";
        }

        return all
            .Where(s => s.Status != "removed" && s.FollowedBy is not { Length: > 0 })
            .Select(s => ToDto(s, sourceOfSuccessor.GetValueOrDefault(s.Gid)))
            .ToList();
    }

    public static DownloadDto ToDto(Aria2Status s, string? knownSource = null)
    {
        var fetchingMetadata = s.Files.Any(f => f.Path.Contains(MetadataPrefix)) || (s.InfoHash is not null && s.Files.Length == 0 && s.TotalLength == 0);
        var isTorrent = s.InfoHash is not null || s.BitTorrent is not null;
        var seeding = isTorrent && s.Status == "active" && s.TotalLength > 0 && s.CompletedLength >= s.TotalLength;
        var status = seeding ? "completed" : s.Status switch
        {
            "active" => "downloading",
            "waiting" => "queued",
            "paused" => "paused",
            "complete" => "completed",
            "error" => "failed",
            _ => "queued",
        };
        var remaining = s.TotalLength - s.CompletedLength;

        return new DownloadDto
        {
            Gid = s.Gid,
            Name = NameOf(s),
            Path = PathOf(s),
            Status = status,
            Source = knownSource ?? SourceOf(s, isTorrent),
            FileType = FileTypes.GetValueOrDefault(System.IO.Path.GetExtension(NameOf(s)), "file"),
            Size = s.TotalLength,
            Downloaded = s.CompletedLength,
            Speed = s.DownloadSpeed,
            UploadSpeed = s.UploadSpeed,
            Eta = s.DownloadSpeed > 0 && remaining > 0 ? remaining / s.DownloadSpeed : null,
            Connections = s.Connections,
            Seeders = s.NumSeeders,
            FetchingMetadata = fetchingMetadata,
            Seeding = seeding,
            Error = status == "failed" ? (string.IsNullOrWhiteSpace(s.ErrorMessage) ? null : s.ErrorMessage) : null,
        };
    }

    private static string SourceOf(Aria2Status s, bool isTorrent)
    {
        if (isTorrent) return s.Following is not null ? "magnet" : "torrent";
        var uri = s.Files.SelectMany(f => f.Uris).Select(u => u.Uri).FirstOrDefault();
        return Uri.TryCreate(uri, UriKind.Absolute, out var u) && u.Scheme is "https" or "ftp" ? u.Scheme : "http";
    }

    private static string NameOf(Aria2Status s)
    {
        if (s.BitTorrent?.Info?.Name is { Length: > 0 } torrentName && s.Files.Length != 1) return torrentName;
        var path = s.Files.Select(f => f.Path).FirstOrDefault(p => p.Length > 0);
        if (path is not null)
        {
            var name = System.IO.Path.GetFileName(path);
            return name.StartsWith(MetadataPrefix) ? name[MetadataPrefix.Length..] : name;
        }
        if (s.BitTorrent?.Info?.Name is { Length: > 0 } n) return n;
        if (s.InfoHash is not null) return s.InfoHash;
        var uri = s.Files.SelectMany(f => f.Uris).Select(u => u.Uri).FirstOrDefault() ?? s.Gid;
        return Uri.TryCreate(uri, UriKind.Absolute, out var parsed) && System.IO.Path.GetFileName(parsed.AbsolutePath) is { Length: > 0 } file ? file : uri;
    }

    private static string PathOf(Aria2Status s)
    {
        if (s.BitTorrent?.Info?.Name is { Length: > 0 } name && s.Files.Length > 1 && s.Dir is not null)
            return System.IO.Path.Combine(s.Dir, name);
        return s.Files.Select(f => f.Path).FirstOrDefault(p => p.Length > 0) ?? s.Dir ?? "";
    }
}
