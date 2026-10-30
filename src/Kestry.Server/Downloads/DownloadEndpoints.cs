using Kestry.Server.Aria2;

namespace Kestry.Server.Downloads;

public static class DownloadEndpoints
{
    public const int MaxTorrentBytes = 2 * 1024 * 1024;
    public const int MaxTorrentFiles = 50;

    public const int MaxLinksTextLength = 512 * 1024;
    public const int MaxLinks = 2000;

    public record LinksRequest(string? Text, string? Subfolder, bool? Paused);
    public record LinkResult(string Text, string Kind, string State, string? Gid = null, string? Error = null);
    public record LinksResponse(IReadOnlyList<LinkResult> Links, int Added, int Duplicates, int Invalid);

    public record TorrentResult(string Name, string Status, string? Gid = null, string? Error = null);

    public static void MapDownloadEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api/downloads").AddEndpointFilter(MapErrors);

        api.MapPost("/links/preview", async (LinksRequest request, DownloadService svc, CancellationToken ct) =>
        {
            if (Validate(request) is { } error) return error;
            var links = LinkParser.Parse(request.Text!, await svc.ExistingKeysAsync(ct));
            return Results.Ok(Summarize(links.Select(l => new LinkResult(l.Text, l.Kind.ToString().ToLowerInvariant(), l.State.ToString().ToLowerInvariant()))));
        });

        api.MapPost("/links", async (LinksRequest request, DownloadService svc, CancellationToken ct) =>
        {
            if (Validate(request) is { } error) return error;
            var results = new List<LinkResult>();
            foreach (var link in LinkParser.Parse(request.Text!, await svc.ExistingKeysAsync(ct)))
            {
                var (kind, state) = (link.Kind.ToString().ToLowerInvariant(), link.State.ToString().ToLowerInvariant());
                if (link.State != LinkState.Valid)
                {
                    results.Add(new(link.Text, kind, state));
                    continue;
                }
                try
                {
                    results.Add(new(link.Text, kind, "added", await svc.AddUriAsync(link.Text, request.Subfolder, request.Paused ?? false, ct)));
                }
                catch (Aria2Exception ex)
                {
                    results.Add(new(link.Text, kind, "invalid", Error: ex.Message));
                }
            }
            return Results.Ok(Summarize(results));
        });

        api.MapPost("/torrents", async (IFormFileCollection files, string? subfolder, bool? paused, DownloadService svc, CancellationToken ct) =>
        {
            if (files.Count is 0 or > MaxTorrentFiles) return Results.BadRequest(new { error = $"Send between 1 and {MaxTorrentFiles} .torrent files." });
            var results = new List<TorrentResult>();
            foreach (var file in files)
            {
                if (file.Length is 0 or > MaxTorrentBytes || !file.FileName.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new(file.FileName, "invalid", Error: $"Only .torrent files up to {MaxTorrentBytes / 1024 / 1024} MB are accepted."));
                    continue;
                }
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                var bytes = ms.ToArray();
                // A .torrent is a bencoded dictionary; cheap sanity check before bothering aria2.
                if (bytes[0] != (byte)'d')
                {
                    results.Add(new(file.FileName, "invalid", Error: "Not a valid .torrent file."));
                    continue;
                }
                try
                {
                    results.Add(new(file.FileName, "added", await svc.AddTorrentAsync(bytes, subfolder, paused ?? false, ct)));
                }
                catch (Aria2Exception ex)
                {
                    results.Add(new(file.FileName, "invalid", Error: ex.Message));
                }
            }
            return Results.Ok(results);
        }).DisableAntiforgery();

        api.MapPost("/{gid}/pause", async (string gid, DownloadService svc, CancellationToken ct) => { await svc.PauseAsync(gid, ct); return Results.NoContent(); });
        api.MapPost("/{gid}/resume", async (string gid, DownloadService svc, CancellationToken ct) => { await svc.ResumeAsync(gid, ct); return Results.NoContent(); });
        api.MapPost("/{gid}/retry", async (string gid, DownloadService svc, CancellationToken ct) => Results.Ok(new { gid = await svc.RetryAsync(gid, ct) }));
        api.MapDelete("/{gid}", async (string gid, bool? deleteFiles, DownloadService svc, CancellationToken ct) =>
        {
            await svc.RemoveAsync(gid, deleteFiles ?? false, ct);
            return Results.NoContent();
        });
        api.MapPost("/pause-all", async (DownloadService svc, CancellationToken ct) => { await svc.PauseAllAsync(ct); return Results.NoContent(); });
        api.MapPost("/resume-all", async (DownloadService svc, CancellationToken ct) => { await svc.ResumeAllAsync(ct); return Results.NoContent(); });
        api.MapPost("/purge", async (DownloadService svc, CancellationToken ct) => { await svc.PurgeAsync(ct); return Results.NoContent(); });
    }

    private static IResult? Validate(LinksRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Text)) return Results.BadRequest(new { error = "Paste at least one link." });
        if (request.Text.Length > MaxLinksTextLength) return Results.BadRequest(new { error = "The pasted text is too large." });
        if (request.Text.Count(c => c == '\n') > MaxLinks) return Results.BadRequest(new { error = $"At most {MaxLinks} links per request." });
        return null;
    }

    private static LinksResponse Summarize(IEnumerable<LinkResult> links)
    {
        var list = links.ToList();
        return new(list, list.Count(l => l.State is "valid" or "added"), list.Count(l => l.State == "duplicate"), list.Count(l => l.State == "invalid"));
    }

    private static async ValueTask<object?> MapErrors(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        try
        {
            return await next(ctx);
        }
        catch (InvalidDownloadException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (Aria2Exception ex) when (ex.Code == 1) // "GID is not found"
        {
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Aria2Exception ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status502BadGateway);
        }
        catch (HttpRequestException)
        {
            return Results.Json(new { error = "aria2 is not available." }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
