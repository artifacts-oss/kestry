using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace Kestry.Server.Tests.Smoke;

/// <summary>Serves /small.bin (fast) and /slow.bin (trickles) on a random loopback port.</summary>
public sealed class LocalFileServer : IAsyncDisposable
{
    public const int SmallSize = 256 * 1024;
    public static readonly byte[] Small = Enumerable.Range(0, SmallSize).Select(i => (byte)(i % 251)).ToArray();

    private readonly WebApplication _app;
    public string BaseUrl { get; }

    private LocalFileServer(WebApplication app, string baseUrl) => (_app, BaseUrl) = (app, baseUrl);

    public static async Task<LocalFileServer> StartAsync()
    {
        var port = KestryFactory.FreePort();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls($"http://127.0.0.1:{port}");
        var app = builder.Build();
        app.MapGet("/health", () => "ok");
        app.MapGet("/small.bin", () => Results.File(Small, "application/octet-stream"));
        app.MapGet("/slow.bin", async (HttpContext ctx) =>
        {
            // Honour "Range: bytes=N-" so aria2 can resume after a pause instead of failing on a full 200 reply.
            const long total = 50 * 1024 * 1024;
            long start = 0;
            if (ctx.Request.Headers.Range.ToString() is { Length: > 0 } range && long.TryParse(range.Replace("bytes=", "").Split('-')[0], out start))
            {
                ctx.Response.StatusCode = StatusCodes.Status206PartialContent;
                ctx.Response.Headers.ContentRange = $"bytes {start}-{total - 1}/{total}";
            }
            ctx.Response.Headers.AcceptRanges = "bytes";
            ctx.Response.ContentLength = total - start;
            var chunk = new byte[16 * 1024];
            try
            {
                while (!ctx.RequestAborted.IsCancellationRequested)
                {
                    await ctx.Response.Body.WriteAsync(chunk, ctx.RequestAborted);
                    await Task.Delay(100, ctx.RequestAborted);
                }
            }
            catch (OperationCanceledException) { /* client went away */ }
        });
        await app.StartAsync();
        return new LocalFileServer(app, $"http://127.0.0.1:{port}");
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
