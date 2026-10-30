using System.Net;
using System.Text;
using System.Text.Json;
using Kestry.Server.Aria2;

namespace Kestry.Server.Tests.Aria2;

public class Aria2ClientTests
{
    private sealed class FakeHandler(Func<JsonElement, (HttpStatusCode, string)> respond) : HttpMessageHandler
    {
        public List<JsonElement> Requests { get; } = [];
        public Uri? LastUri { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            LastUri = request.RequestUri;
            var json = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)).RootElement.Clone();
            Requests.Add(json);
            var (status, body) = respond(json);
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
    }

    private static (Aria2Client, FakeHandler, Aria2Runtime) Create(Func<JsonElement, (HttpStatusCode, string)> respond)
    {
        var handler = new FakeHandler(respond);
        var runtime = new Aria2Runtime { Port = 6999 };
        return (new Aria2Client(new HttpClient(handler), runtime), handler, runtime);
    }

    private static (HttpStatusCode, string) Ok(string resultJson) => (HttpStatusCode.OK, $$"""{"id":"kestry","jsonrpc":"2.0","result":{{resultJson}}}""");

    [Fact]
    public async Task Sends_secret_as_first_param_to_loopback_port()
    {
        var (client, handler, runtime) = Create(_ => Ok("\"2089b05ecca3d829\""));

        var gid = await client.AddUriAsync(["http://example.com/a.zip"], new Dictionary<string, string> { ["dir"] = "/d" });

        Assert.Equal("2089b05ecca3d829", gid);
        Assert.Equal("http://127.0.0.1:6999/jsonrpc", handler.LastUri!.ToString());
        var req = handler.Requests.Single();
        Assert.Equal("aria2.addUri", req.GetProperty("method").GetString());
        var p = req.GetProperty("params");
        Assert.Equal($"token:{runtime.Secret}", p[0].GetString());
        Assert.Equal("http://example.com/a.zip", p[1][0].GetString());
        Assert.Equal("/d", p[2].GetProperty("dir").GetString());
    }

    [Fact]
    public async Task AddTorrent_sends_base64_payload()
    {
        var (client, handler, _) = Create(_ => Ok("\"gid1\""));

        await client.AddTorrentAsync([1, 2, 3]);

        Assert.Equal(Convert.ToBase64String([1, 2, 3]), handler.Requests.Single().GetProperty("params")[1].GetString());
    }

    [Fact]
    public async Task TellStatus_parses_numbers_sent_as_strings()
    {
        var (client, _, _) = Create(_ => Ok("""
            {"gid":"g","status":"active","totalLength":"1000","completedLength":"250","downloadSpeed":"10",
             "uploadSpeed":"2","connections":"4","numSeeders":"1","followedBy":["g2"],
             "files":[{"index":"1","path":"/d/a","length":"1000","completedLength":"250"}],
             "bittorrent":{"info":{"name":"Ubuntu"}}}
            """));

        var s = await client.TellStatusAsync("g");

        Assert.Equal(1000, s.TotalLength);
        Assert.Equal(250, s.CompletedLength);
        Assert.Equal(4, s.Connections);
        Assert.Equal(["g2"], s.FollowedBy);
        Assert.Equal("/d/a", s.Files.Single().Path);
        Assert.Equal("Ubuntu", s.BitTorrent!.Info!.Name);
    }

    [Fact]
    public async Task Rpc_errors_become_Aria2Exception()
    {
        var (client, _, _) = Create(_ => (HttpStatusCode.BadRequest, """{"id":"kestry","jsonrpc":"2.0","error":{"code":1,"message":"GID is not found"}}"""));

        var ex = await Assert.ThrowsAsync<Aria2Exception>(() => client.PauseAsync("nope"));

        Assert.Equal(1, ex.Code);
        Assert.Equal("GID is not found", ex.Message);
    }

    [Fact]
    public async Task Multicall_adds_the_secret_to_each_call_and_unwraps_results()
    {
        var (client, handler, runtime) = Create(_ => Ok("""[[[{"gid":"a"}]],[{"downloadSpeed":"5"}]]"""));

        var results = await client.MulticallAsync([Aria2Client.TellActive(), Aria2Client.GlobalStat()]);

        Assert.Equal(2, results.Length);
        Assert.Equal("a", results[0][0].GetProperty("gid").GetString());
        Assert.Equal("5", results[1].GetProperty("downloadSpeed").GetString());
        var calls = handler.Requests.Single().GetProperty("params")[1];
        Assert.Equal("aria2.tellActive", calls[0].GetProperty("methodName").GetString());
        Assert.Equal($"token:{runtime.Secret}", calls[0].GetProperty("params")[0].GetString());
        Assert.Equal($"token:{runtime.Secret}", calls[1].GetProperty("params")[0].GetString());
    }

    [Fact]
    public async Task Multicall_surfaces_a_failed_sub_call()
    {
        var (client, _, _) = Create(_ => Ok("""[{"code":1,"message":"boom"}]"""));

        var ex = await Assert.ThrowsAsync<Aria2Exception>(() => client.MulticallAsync([Aria2Client.GlobalStat()]));

        Assert.Equal("boom", ex.Message);
    }
}
