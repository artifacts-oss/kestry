namespace Kestry.Server.Health;

/// <summary>`Kestry.Server --healthcheck`: lets a container probe /health without curl in the image.</summary>
public static class HealthCheck
{
    public const string Flag = "--healthcheck";

    /// <summary>The probe URL for a "Urls" value: wildcard and any-address hosts become loopback.</summary>
    public static string UrlFor(string? urls)
    {
        var first = (urls ?? "http://127.0.0.1:8080").Split(';', StringSplitOptions.RemoveEmptyEntries)[0];
        var uri = new Uri(first.Replace("://+", "://127.0.0.1").Replace("://*", "://127.0.0.1").Replace("://0.0.0.0", "://127.0.0.1"));
        return $"{uri.Scheme}://{uri.Host}:{uri.Port}/health";
    }

    public static async Task<int> RunAsync(string? urls)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
            using var response = await http.GetAsync(UrlFor(urls));
            return response.IsSuccessStatusCode ? 0 : 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return 1;
        }
    }
}
