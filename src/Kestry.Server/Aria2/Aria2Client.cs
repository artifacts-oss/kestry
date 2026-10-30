using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Kestry.Server.Aria2;

/// <summary>Stateless JSON-RPC client for the local aria2 process; every call is an independent HTTP POST.</summary>
public sealed class Aria2Client(HttpClient http, Aria2Runtime runtime)
{
    // Keys requested from tell*: only what the UI needs.
    public static readonly string[] StatusKeys =
    [
        "gid", "status", "totalLength", "completedLength", "downloadSpeed", "uploadSpeed", "connections",
        "numSeeders", "errorCode", "errorMessage", "dir", "infoHash", "followedBy", "following", "belongsTo",
        "files", "bittorrent",
    ];

    public record Call(string Method, params object?[] Params);

    private string Url => $"http://127.0.0.1:{runtime.Port}/jsonrpc";

    private JsonObject Request(Call call) => new()
    {
        ["jsonrpc"] = "2.0",
        ["id"] = "kestry",
        ["method"] = call.Method,
        ["params"] = JsonSerializer.SerializeToNode(call.Params.Prepend($"token:{runtime.Secret}"), Aria2Json.Options),
    };

    private static void ThrowIfError(JsonElement error) =>
        throw new Aria2Exception(error.GetProperty("code").GetInt32(), error.GetProperty("message").GetString() ?? "");

    public async Task<JsonElement> CallAsync(Call call, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync(Url, Request(call), ct);
        // aria2 reports RPC errors with a non-2xx status and a JSON body.
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(ct);
        if (body.TryGetProperty("error", out var error)) ThrowIfError(error);
        response.EnsureSuccessStatusCode();
        return body.GetProperty("result");
    }

    private async Task<T> CallAsync<T>(Call call, CancellationToken ct) =>
        (await CallAsync(call, ct)).Deserialize<T>(Aria2Json.Options)!;

    /// <summary>Runs several calls in one request (system.multicall); results come back in order.</summary>
    public async Task<JsonElement[]> MulticallAsync(IReadOnlyList<Call> calls, CancellationToken ct = default)
    {
        var batch = calls.Select(c => new JsonObject
        {
            ["methodName"] = c.Method,
            ["params"] = JsonSerializer.SerializeToNode(c.Params.Prepend($"token:{runtime.Secret}"), Aria2Json.Options),
        });
        var result = await CallAsync(new Call("system.multicall", new JsonArray(batch.ToArray<JsonNode?>())), ct);
        return result.EnumerateArray().Select(item =>
        {
            // Success is wrapped in a one-element array, failure is an object with code/message.
            if (item.ValueKind == JsonValueKind.Object) ThrowIfError(item);
            return item[0];
        }).ToArray();
    }

    public Task<string> AddUriAsync(string[] uris, IDictionary<string, string>? options = null, CancellationToken ct = default) =>
        CallAsync<string>(new("aria2.addUri", uris, options ?? new Dictionary<string, string>()), ct);

    public Task<string> AddTorrentAsync(byte[] torrent, IDictionary<string, string>? options = null, CancellationToken ct = default) =>
        CallAsync<string>(new("aria2.addTorrent", Convert.ToBase64String(torrent), Array.Empty<string>(), options ?? new Dictionary<string, string>()), ct);

    public Task PauseAsync(string gid, CancellationToken ct = default) => CallAsync(new("aria2.pause", gid), ct);
    public Task UnpauseAsync(string gid, CancellationToken ct = default) => CallAsync(new("aria2.unpause", gid), ct);
    public Task PauseAllAsync(CancellationToken ct = default) => CallAsync(new("aria2.pauseAll"), ct);
    public Task UnpauseAllAsync(CancellationToken ct = default) => CallAsync(new("aria2.unpauseAll"), ct);
    public Task ForceRemoveAsync(string gid, CancellationToken ct = default) => CallAsync(new("aria2.forceRemove", gid), ct);
    public Task RemoveDownloadResultAsync(string gid, CancellationToken ct = default) => CallAsync(new("aria2.removeDownloadResult", gid), ct);
    public Task PurgeDownloadResultAsync(CancellationToken ct = default) => CallAsync(new("aria2.purgeDownloadResult"), ct);

    public Task<Aria2Status> TellStatusAsync(string gid, CancellationToken ct = default) =>
        CallAsync<Aria2Status>(new("aria2.tellStatus", gid, StatusKeys), ct);

    public Task<Aria2GlobalStat> GetGlobalStatAsync(CancellationToken ct = default) =>
        CallAsync<Aria2GlobalStat>(new("aria2.getGlobalStat"), ct);

    public Task ChangeGlobalOptionAsync(IDictionary<string, string> options, CancellationToken ct = default) =>
        CallAsync(new("aria2.changeGlobalOption", options), ct);

    public Task SaveSessionAsync(CancellationToken ct = default) => CallAsync(new("aria2.saveSession"), ct);
    public Task ShutdownAsync(CancellationToken ct = default) => CallAsync(new("aria2.shutdown"), ct);

    public async Task<string> GetVersionAsync(CancellationToken ct = default) =>
        (await CallAsync(new("aria2.getVersion"), ct)).GetProperty("version").GetString() ?? "";

    public static Call TellActive() => new("aria2.tellActive", StatusKeys);
    public static Call TellWaiting(int offset, int count) => new("aria2.tellWaiting", offset, count, StatusKeys);
    public static Call TellStopped(int offset, int count) => new("aria2.tellStopped", offset, count, StatusKeys);
    public static Call GlobalStat() => new("aria2.getGlobalStat");
}
