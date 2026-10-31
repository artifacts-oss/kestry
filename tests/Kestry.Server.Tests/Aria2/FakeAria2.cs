using System.Net;
using System.Text;
using System.Text.Json;
using Kestry.Server.Aria2;

namespace Kestry.Server.Tests.Aria2;

/// <summary>An HttpMessageHandler that plays aria2: records calls and answers from a per-method table.</summary>
public sealed class FakeAria2 : HttpMessageHandler
{
    private readonly Dictionary<string, string?> _results = new();
    public List<(string Method, JsonElement Params)> Calls { get; } = [];
    public Aria2Client Client { get; }

    public FakeAria2() => Client = new Aria2Client(new HttpClient(this), new Aria2Runtime());

    public FakeAria2 On(string method, string? resultJson)
    {
        _results[method] = resultJson;
        return this;
    }

    /// <summary>Answers <paramref name="method"/> with aria2's "GID is not found" error.</summary>
    public FakeAria2 NotFound(string method) => On(method, null);

    public string[] Methods => Calls.Select(c => c.Method).ToArray();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Assert.NotNull(request.Content!.Headers.ContentLength); // aria2 cannot parse chunked request bodies
        var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
        var method = json.GetProperty("method").GetString()!;
        Calls.Add((method, json.GetProperty("params")));
        var answer = _results.TryGetValue(method, out var result) && result is null
            ? """{"id":"kestry","jsonrpc":"2.0","error":{"code":1,"message":"GID is not found"}}"""
            : $$"""{"id":"kestry","jsonrpc":"2.0","result":{{result ?? "\"OK\""}}}""";
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(answer, Encoding.UTF8, "application/json") };
    }
}
