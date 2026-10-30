using System.Net;
using System.Net.Http.Json;

namespace Kestry.Server.Tests;

public class HealthTests : IClassFixture<KestryFactory>
{
    private readonly KestryFactory _factory;

    public HealthTests(KestryFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_returns_ok_with_the_aria2_state()
    {
        var response = await _factory.CreateClient().GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
        Assert.Equal("ok", body!["status"]);
        Assert.Contains(body["aria2"], new[] { "installing", "running", "restarting", "down" });
    }
}
