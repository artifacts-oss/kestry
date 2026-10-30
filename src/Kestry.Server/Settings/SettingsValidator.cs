using Kestry.Server.Downloads;

namespace Kestry.Server.Settings;

public sealed class SettingsValidationException(IReadOnlyDictionary<string, ErrorBody> errors) : Exception("Invalid settings")
{
    public IReadOnlyDictionary<string, ErrorBody> Errors { get; } = errors;
}

public static class SettingsValidator
{
    public const int MaxLimitKBps = 100 * 1024 * 1024; // 100 GB/s, i.e. "unreasonably large" guard only

    /// <summary>Returns field → error for every invalid value (empty when valid).</summary>
    public static Dictionary<string, ErrorBody> Validate(AppSettings s, KestryOptions options)
    {
        var errors = new Dictionary<string, ErrorBody>();
        if (s.MaxConcurrentDownloads is < 1 or > 20) errors["maxConcurrentDownloads"] = new(ErrorCodes.MaxConcurrentRange, "Must be between 1 and 20.");
        if (s.DownloadLimitKBps is < 1 or > MaxLimitKBps) errors["downloadLimitKBps"] = new(ErrorCodes.LimitMin, "Must be at least 1 KB/s.");
        if (s.UploadLimitKBps is < 1 or > MaxLimitKBps) errors["uploadLimitKBps"] = new(ErrorCodes.LimitMin, "Must be at least 1 KB/s.");
        if (s.SeedingEnabled && (double.IsNaN(s.SeedRatio) || s.SeedRatio is < 0.1 or > 100)) errors["seedRatio"] = new(ErrorCodes.SeedRatioRange, "Must be between 0.1 and 100.");

        if (string.IsNullOrWhiteSpace(s.DownloadDir) || !Path.IsPathFullyQualified(s.DownloadDir))
            errors["downloadDir"] = new(ErrorCodes.DirNotAbsolute, "Must be an absolute path.");
        else if (!PathGuard.IsUnderAny(s.DownloadDir, options.EffectiveRoots))
            errors["downloadDir"] = new(ErrorCodes.DirOutsideRoots, "Must be inside one of the allowed folders.");
        return errors;
    }

    /// <summary>Creates the folder if needed and proves it is writable. Only call after <see cref="Validate"/> passed.</summary>
    public static ErrorBody? CheckWritable(string dir)
    {
        try
        {
            Directory.CreateDirectory(dir);
            var probe = Path.Combine(dir, $".kestry-write-test-{Guid.NewGuid():N}");
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new(ErrorCodes.DirNotWritable, "The folder cannot be created or is not writable.");
        }
    }
}
