using System.Net;
using System.Net.Http.Json;
using Kestry.Server.Downloads;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kestry.Server.Tests.Security;

public sealed class TunnelFactory : KestryFactory
{
    protected override IEnumerable<KeyValuePair<string, string?>> Extra =>
        [new("Kestry:AllowedOrigins:0", "https://kestry.example.com")];
}

public class RequestGuardTests(TunnelFactory factory) : IClassFixture<TunnelFactory>
{
    private static HttpRequestMessage Req(HttpMethod method, string url, string? host = null, string? origin = null, HttpContent? content = null)
    {
        var r = new HttpRequestMessage(method, url) { Content = content };
        if (host is not null) r.Headers.Host = host;
        if (origin is not null) r.Headers.TryAddWithoutValidation("Origin", origin);
        return r;
    }

    private async Task<HttpStatusCode> Send(HttpRequestMessage r) => (await factory.CreateClient().SendAsync(r)).StatusCode;

    [Theory]
    [InlineData("localhost")]
    [InlineData("localhost:8080")]
    [InlineData("127.0.0.1:8080")]
    [InlineData("192.168.1.20:8080")]
    [InlineData("[::1]:8080")]
    [InlineData("kestry.example.com")]
    public async Task Allows_loopback_ip_and_configured_hosts(string host) =>
        Assert.Equal(HttpStatusCode.OK, await Send(Req(HttpMethod.Get, "/api/system", host)));

    [Theory]
    [InlineData("evil.com")]
    [InlineData("evil.com:8080")]
    [InlineData("nas.local")]
    [InlineData("kestry.example.com.evil.com")]
    public async Task Rejects_unknown_host_names_which_is_how_dns_rebinding_arrives(string host)
    {
        var response = await factory.CreateClient().SendAsync(Req(HttpMethod.Get, "/api/system", host));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains("AllowedOrigins", await response.Content.ReadAsStringAsync());
        Assert.Equal(ErrorCodes.HostNotAllowed, (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Code);
    }

    [Fact]
    public async Task The_health_check_and_the_page_ignore_the_guard() =>
        Assert.Equal(HttpStatusCode.OK, await Send(Req(HttpMethod.Get, "/health", "evil.com")));

    [Theory]
    [InlineData("http://evil.com")]
    [InlineData("http://localhost:9999")] // same host, other port: another app
    [InlineData("null")]
    public async Task Rejects_mutating_requests_from_foreign_origins(string origin)
    {
        var response = await factory.CreateClient().SendAsync(Req(HttpMethod.Post, "/api/shutdown/cancel", origin: origin));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal(ErrorCodes.OriginNotAllowed, (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Code);
    }

    [Theory]
    [InlineData(null)]                               // curl and other non-browser clients
    [InlineData("http://localhost")]                 // the page itself
    [InlineData("https://kestry.example.com")]       // configured tunnel domain
    [InlineData("https://KESTRY.example.com/")]
    public async Task Accepts_mutating_requests_from_the_page_or_an_allowed_origin(string? origin) =>
        Assert.Equal(HttpStatusCode.NoContent, await Send(Req(HttpMethod.Post, "/api/shutdown/cancel", host: origin is null ? null : new Uri(origin).Authority, origin: origin)));

    [Fact]
    public async Task Rejects_foreign_origin_on_every_mutating_method()
    {
        foreach (var method in new[] { HttpMethod.Put, HttpMethod.Delete, HttpMethod.Post })
            Assert.Equal(HttpStatusCode.Forbidden, await Send(Req(method, "/api/downloads/abc", origin: "http://evil.com")));
    }

    [Fact]
    public async Task Reads_do_not_depend_on_origin() =>
        Assert.Equal(HttpStatusCode.OK, await Send(Req(HttpMethod.Get, "/api/system", origin: "http://evil.com")));

    [Fact]
    public async Task Request_size_limits_are_configured_for_json_and_torrent_uploads()
    {
        // TestServer does not enforce Kestrel limits, so check the configuration; the real 413 was verified against Kestrel.
        var limits = factory.Services.GetRequiredService<IOptions<KestrelServerOptions>>().Value.Limits;
        Assert.Equal(DownloadEndpoints.MaxRequestBytes, limits.MaxRequestBodySize);

        var torrents = factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .Single(e => e.DisplayName!.Contains("/torrents"));
        Assert.Equal(DownloadEndpoints.MaxTorrentUploadBytes, torrents.Metadata.GetMetadata<IRequestSizeLimitMetadata>()!.MaxRequestBodySize);
    }

    [Fact]
    public async Task Oversized_torrent_files_are_flagged_invalid()
    {
        var threeMb = new MultipartFormDataContent { { new ByteArrayContent(new byte[3 * 1024 * 1024]), "files", "big.torrent" } };

        var ok = await factory.CreateClient().PostAsync("/api/downloads/torrents", threeMb);

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var results = await ok.Content.ReadFromJsonAsync<DownloadEndpoints.TorrentResult[]>();
        Assert.Equal("invalid", results![0].Status);
        Assert.Equal(ErrorCodes.TorrentsTooLarge, results[0].Error!.Code);
        Assert.Equal(2, Convert.ToInt32(results[0].Error!.Params!["maxMb"].ToString()));
    }
}
