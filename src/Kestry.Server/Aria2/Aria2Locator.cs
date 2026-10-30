using System.IO.Compression;
using System.Security.Cryptography;

namespace Kestry.Server.Aria2;

public sealed class Aria2LocateException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>Pinned official Windows build; bump version, URL and hash together by hand.</summary>
public static class Aria2Release
{
    public const string Version = "1.37.0";
    public const string ZipEntry = "aria2-1.37.0-win-64bit-build1/aria2c.exe";
    public const string Sha256 = "67d015301eef0b612191212d564c5bb0a14b5b9c4796b76454276a4d28d9b288";
    public const string Url = "https://github.com/aria2/aria2/releases/download/release-1.37.0/aria2-1.37.0-win-64bit-build1.zip";
}

/// <summary>
/// Finds aria2c (configured path, then PATH, then DataDir/aria2). Only on Windows, if it is missing, it downloads the
/// pinned official build and verifies its SHA-256 before extracting anything.
/// </summary>
public sealed class Aria2Locator(
    string dataDir,
    string? configuredPath,
    string? pathEnv,
    bool isWindows,
    Func<CancellationToken, Task<byte[]>> download,
    string expectedSha256 = Aria2Release.Sha256,
    string zipEntry = Aria2Release.ZipEntry)
{
    private string ExeName => isWindows ? "aria2c.exe" : "aria2c";
    private string InstallDir => Path.Combine(dataDir, "aria2");

    public string? Find()
    {
        if (!string.IsNullOrWhiteSpace(configuredPath))
        {
            return File.Exists(configuredPath)
                ? configuredPath
                : throw new Aria2LocateException("notFound", $"Aria2:Path points to a missing file: {configuredPath}");
        }

        var dirs = (pathEnv ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var dir in dirs.Append(InstallDir))
        {
            var candidate = Path.Combine(dir.Trim('"'), ExeName);
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    /// <param name="onInstalling">Called right before a download starts so the UI can show "Installing aria2…".</param>
    public async Task<string> ResolveAsync(Action onInstalling, CancellationToken ct = default)
    {
        if (Find() is { } found) return found;

        if (!isWindows)
        {
            throw new Aria2LocateException("notFound",
                "aria2c was not found. Install it with your package manager (apt install aria2, dnf install aria2…) or set Aria2:Path.");
        }

        onInstalling();
        byte[] zip;
        try
        {
            zip = await download(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or IOException)
        {
            throw new Aria2LocateException("downloadFailed",
                $"Could not download aria2 ({ex.Message}). Copy aria2c.exe to {InstallDir} by hand or set Aria2:Path.");
        }

        var actual = Convert.ToHexString(SHA256.HashData(zip)).ToLowerInvariant();
        if (!string.Equals(actual, expectedSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new Aria2LocateException("checksumMismatch",
                $"The downloaded aria2 archive has an unexpected SHA-256 ({actual}); it was discarded. Copy aria2c.exe to {InstallDir} by hand or set Aria2:Path.");
        }

        using var archive = new ZipArchive(new MemoryStream(zip));
        var entry = archive.GetEntry(zipEntry)
            ?? throw new Aria2LocateException("downloadFailed", $"{zipEntry} is missing from the aria2 archive.");
        Directory.CreateDirectory(InstallDir);
        var target = Path.Combine(InstallDir, ExeName);
        entry.ExtractToFile(target, overwrite: true);
        return target;
    }

    public static async Task<byte[]> DownloadPinnedAsync(HttpClient http, CancellationToken ct) =>
        await http.GetByteArrayAsync(Aria2Release.Url, ct);
}
