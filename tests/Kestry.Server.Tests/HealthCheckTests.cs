using Kestry.Server.Health;

namespace Kestry.Server.Tests;

public class HealthCheckTests
{
    [Theory]
    [InlineData(null, "http://127.0.0.1:8080/health")]
    [InlineData("http://+:8080", "http://127.0.0.1:8080/health")]
    [InlineData("http://*:9000", "http://127.0.0.1:9000/health")]
    [InlineData("http://0.0.0.0:7000", "http://127.0.0.1:7000/health")]
    [InlineData("http://127.0.0.1:8081;https://127.0.0.1:8443", "http://127.0.0.1:8081/health")]
    [InlineData("http://localhost:5000", "http://localhost:5000/health")]
    public void Builds_a_loopback_probe_url(string? urls, string expected) => Assert.Equal(expected, HealthCheck.UrlFor(urls));

    [Fact]
    public async Task Fails_when_nothing_is_listening() =>
        Assert.Equal(1, await HealthCheck.RunAsync($"http://127.0.0.1:{KestryFactory.FreePort()}"));

    [Fact]
    public async Task Succeeds_when_health_answers_200()
    {
        await using var server = await Smoke.LocalFileServer.StartAsync();

        Assert.Equal(0, await HealthCheck.RunAsync(server.BaseUrl));
    }
}
