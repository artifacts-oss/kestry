using Microsoft.AspNetCore.Mvc;
using Kestry.Server.Aria2;

namespace Kestry.Server.Downloads;

public static class DownloadEndpoints
{
    public const int MaxRequestBytes = 2 * 1024 * 1024;
    public const int MaxTorrentBytes = 2 * 1024 * 1024;
    public const int MaxTorrentUploadBytes = 25 * 1024 * 1024;
    public const int MaxTorrentFiles = 50;

    public const int MaxLinksTextLength = 512 * 1024;
    public const int MaxLinks = 2000;

    public record LinksRequest(string? Text, string? Subfolder, bool? Paused);
    public record LinkResult(string Text, string Kind, string State, string? Gid = null, ErrorBody? Error = null);
    /// <summary>Valid counts links that can be added (preview) or were added (POST /links).</summary>
    public record LinksResponse(IReadOnlyList<LinkResult> Links, int Valid, int Duplicates, int Invalid);

    public record TorrentResult(string Name, string Status, string? Gid = null, ErrorBody? Error = null);

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
                    results.Add(new(link.Text, kind, "invalid", Error: Aria2Failure(ex)));
                }
            }
            return Results.Ok(Summarize(results));
        });

        api.MapPost("/torrents", async (IFormFileCollection files, string? subfolder, bool? paused, DownloadService svc, CancellationToken ct) =>
        {
            if (files.Count is 0 or > MaxTorrentFiles) return ApiErrors.BadRequest(new(ErrorCodes.TorrentsCount, $"Send between 1 and {MaxTorrentFiles} .torrent files.", new Dictionary<string, object> { ["max"] = MaxTorrentFiles }));
            var results = new List<TorrentResult>();
            foreach (var file in files)
            {
                if (file.Length == 0 || !file.FileName.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
                {
                    results.Add(new(file.FileName, "invalid", Error: new(ErrorCodes.TorrentsInvalid, "Not a valid .torrent file.")));
                    continue;
                }
                if (file.Length > MaxTorrentBytes)
                {
                    results.Add(new(file.FileName, "invalid", Error: new(ErrorCodes.TorrentsTooLarge, $"Only .torrent files up to {MaxTorrentBytes / 1024 / 1024} MB are accepted.", new Dictionary<string, object> { ["maxMb"] = MaxTorrentBytes / 1024 / 1024 })));
                    continue;
                }
                using var ms = new MemoryStream();
                await file.CopyToAsync(ms, ct);
                var bytes = ms.ToArray();
                // A .torrent is a bencoded dictionary; cheap sanity check before bothering aria2.
                if (bytes[0] != (byte)'d')
                {
                    results.Add(new(file.FileName, "invalid", Error: new(ErrorCodes.TorrentsInvalid, "Not a valid .torrent file.")));
                    continue;
                }
                try
                {
                    results.Add(new(file.FileName, "added", await svc.AddTorrentAsync(bytes, subfolder, paused ?? false, ct)));
                }
                catch (Aria2Exception ex)
                {
                    results.Add(new(file.FileName, "invalid", Error: Aria2Failure(ex)));
                }
            }
            return Results.Ok(results);
        }).DisableAntiforgery().WithMetadata(new RequestSizeLimitAttribute(MaxTorrentUploadBytes));

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
        if (string.IsNullOrWhiteSpace(request.Text)) return ApiErrors.BadRequest(new(ErrorCodes.LinksEmpty, "Paste at least one link."));
        if (request.Text.Length > MaxLinksTextLength) return ApiErrors.BadRequest(new(ErrorCodes.LinksTooLarge, "The pasted text is too large."));
        if (request.Text.Count(c => c == '\n') > MaxLinks) return ApiErrors.BadRequest(new(ErrorCodes.LinksTooMany, $"At most {MaxLinks} links per request.", new Dictionary<string, object> { ["max"] = MaxLinks }));
        return null;
    }

    private static ErrorBody Aria2Failure(Aria2Exception ex) =>
        new(ErrorCodes.Aria2Error, ex.Message, new Dictionary<string, object> { ["message"] = ex.Message });

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
            return ApiErrors.BadRequest(new(ex.Code, ex.Message));
        }
        catch (Aria2Exception ex) when (ex.Code == 1) // "GID is not found"
        {
            return ApiErrors.Json(StatusCodes.Status404NotFound, new(ErrorCodes.NotFound, ex.Message));
        }
        catch (Aria2Exception ex)
        {
            return ApiErrors.Json(StatusCodes.Status502BadGateway, Aria2Failure(ex));
        }
        catch (HttpRequestException)
        {
            return ApiErrors.Json(StatusCodes.Status503ServiceUnavailable, new(ErrorCodes.Aria2Unavailable, "aria2 is not available."));
        }
    }
}
