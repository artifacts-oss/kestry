using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kestry.Server.Aria2;

public enum Aria2State { Installing, Running, Restarting, Down }

/// <summary>Shared connection details and health of the aria2 child process.</summary>
public sealed class Aria2Runtime
{
    public int Port { get; set; } = 6800;
    public string Secret { get; } = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(16));
    public volatile Aria2State State = Aria2State.Down;
    /// <summary>Stable code for the UI to localize (notFound, downloadFailed, checksumMismatch, portBusy, crashed).</summary>
    public string? ReasonCode { get; set; }
    public string? Reason { get; set; }
    public string? Version { get; set; }

    public void SetState(Aria2State state, string? reasonCode = null, string? reason = null)
    {
        (State, ReasonCode, Reason) = (state, reasonCode, reason);
        if (state != Aria2State.Running) Version = null;
    }
}

public sealed class Aria2Exception(int code, string message) : Exception(message)
{
    public int Code { get; } = code;
}

public sealed record Aria2File
{
    public int Index { get; init; }
    public string Path { get; init; } = "";
    public long Length { get; init; }
    public long CompletedLength { get; init; }
}

public sealed record Aria2BitTorrent
{
    public Aria2BtInfo? Info { get; init; }
}

public sealed record Aria2BtInfo
{
    public string? Name { get; init; }
}

public sealed record Aria2Status
{
    public string Gid { get; init; } = "";
    public string Status { get; init; } = "";
    public long TotalLength { get; init; }
    public long CompletedLength { get; init; }
    public long DownloadSpeed { get; init; }
    public long UploadSpeed { get; init; }
    public int Connections { get; init; }
    public int NumSeeders { get; init; }
    public int ErrorCode { get; init; }
    public string? ErrorMessage { get; init; }
    public string? Dir { get; init; }
    public string? InfoHash { get; init; }
    public string[]? FollowedBy { get; init; }
    public string? Following { get; init; }
    public string? BelongsTo { get; init; }
    public Aria2File[] Files { get; init; } = [];
    [JsonPropertyName("bittorrent")] public Aria2BitTorrent? BitTorrent { get; init; }
}

public sealed record Aria2GlobalStat
{
    public long DownloadSpeed { get; init; }
    public long UploadSpeed { get; init; }
    public int NumActive { get; init; }
    public int NumWaiting { get; init; }
    public int NumStopped { get; init; }
}

public static class Aria2Json
{
    // aria2 returns every number as a string.
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
    };
}
