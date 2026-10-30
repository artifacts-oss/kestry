namespace Kestry.Server;

/// <summary>
/// Every error the UI can show: a stable <see cref="Code"/> the frontend translates (Errors namespace in
/// i18n/messages), an English <see cref="Error"/> for logs and non-UI clients, and values for placeholders.
/// </summary>
public sealed record ErrorBody(string Code, string Error, IReadOnlyDictionary<string, object>? Params = null);

public static class ErrorCodes
{
    public const string HostNotAllowed = "request.hostNotAllowed";
    public const string OriginNotAllowed = "request.originNotAllowed";
    public const string LinksEmpty = "links.empty";
    public const string LinksTooLarge = "links.tooLarge";
    public const string LinksTooMany = "links.tooMany";
    public const string TorrentsCount = "torrents.count";
    public const string TorrentsTooLarge = "torrents.tooLarge";
    public const string TorrentsInvalid = "torrents.invalid";
    public const string InvalidSubfolder = "downloads.invalidSubfolder";
    public const string NoSource = "downloads.noSource";
    public const string NotFound = "downloads.notFound";
    public const string Aria2Error = "aria2.error";
    public const string Aria2Unavailable = "aria2.unavailable";
    public const string SettingsInvalid = "settings.invalid";
    public const string MaxConcurrentRange = "settings.maxConcurrentDownloads.range";
    public const string LimitMin = "settings.limit.min";
    public const string SeedRatioRange = "settings.seedRatio.range";
    public const string DirNotAbsolute = "settings.downloadDir.notAbsolute";
    public const string DirOutsideRoots = "settings.downloadDir.outsideRoots";
    public const string DirNotWritable = "settings.downloadDir.notWritable";
    public const string ShutdownUnavailable = "settings.shutdownWhenDone.unavailable";
}

public static class ApiErrors
{
    public static IResult Json(int status, ErrorBody body) => Results.Json(body, statusCode: status);

    public static IResult BadRequest(ErrorBody body) => Json(StatusCodes.Status400BadRequest, body);
}
