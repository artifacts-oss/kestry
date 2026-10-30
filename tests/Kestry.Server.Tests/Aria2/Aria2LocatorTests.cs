using System.IO.Compression;
using System.Security.Cryptography;
using Kestry.Server.Aria2;

namespace Kestry.Server.Tests.Aria2;

public class Aria2LocatorTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("kestry-locator").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    private string Touch(params string[] parts)
    {
        var path = Path.Combine([_root, .. parts]);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "bin");
        return path;
    }

    private Aria2Locator Locator(string? configured = null, string? pathEnv = null, bool windows = false,
        Func<CancellationToken, Task<byte[]>>? download = null, string sha = "", string entry = "x/aria2c.exe") =>
        new(Path.Combine(_root, "data"), configured, pathEnv, windows,
            download ?? (_ => throw new InvalidOperationException("no download expected")), sha, entry);

    private static byte[] Zip(string entryName, string content)
    {
        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        using (var w = new StreamWriter(zip.CreateEntry(entryName).Open())) w.Write(content);
        return ms.ToArray();
    }

    private static string Sha(byte[] data) => Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();

    [Fact]
    public void Configured_path_wins_over_PATH()
    {
        var configured = Touch("custom", "aria2c");
        Touch("bin", "aria2c");

        Assert.Equal(configured, Locator(configured, Path.Combine(_root, "bin")).Find());
    }

    [Fact]
    public void Configured_path_that_does_not_exist_is_an_error_not_a_fallthrough()
    {
        Touch("bin", "aria2c");

        var ex = Assert.Throws<Aria2LocateException>(() => Locator(Path.Combine(_root, "missing"), Path.Combine(_root, "bin")).Find());

        Assert.Equal("notFound", ex.Code);
    }

    [Fact]
    public void PATH_is_searched_before_the_data_dir()
    {
        var onPath = Touch("bin", "aria2c");
        Touch("data", "aria2", "aria2c");

        Assert.Equal(onPath, Locator(pathEnv: Path.Combine(_root, "bin")).Find());
    }

    [Fact]
    public void Data_dir_copy_is_used_when_not_on_PATH()
    {
        var installed = Touch("data", "aria2", "aria2c");

        Assert.Equal(installed, Locator(pathEnv: "").Find());
    }

    [Fact]
    public async Task Linux_without_aria2_reports_install_instructions_and_never_downloads()
    {
        var ex = await Assert.ThrowsAsync<Aria2LocateException>(() => Locator(pathEnv: "").ResolveAsync(() => { }));

        Assert.Equal("notFound", ex.Code);
        Assert.Contains("apt install aria2", ex.Message);
    }

    [Fact]
    public async Task Windows_downloads_verifies_and_extracts()
    {
        var zip = Zip("x/aria2c.exe", "MZ-fake");
        var installing = false;

        var path = await Locator(pathEnv: "", windows: true, download: _ => Task.FromResult(zip), sha: Sha(zip))
            .ResolveAsync(() => installing = true);

        Assert.True(installing);
        Assert.Equal(Path.Combine(_root, "data", "aria2", "aria2c.exe"), path);
        Assert.Equal("MZ-fake", File.ReadAllText(path));
    }

    [Fact]
    public async Task Windows_rejects_an_archive_with_a_different_hash_and_extracts_nothing()
    {
        var zip = Zip("x/aria2c.exe", "evil");

        var ex = await Assert.ThrowsAsync<Aria2LocateException>(() =>
            Locator(pathEnv: "", windows: true, download: _ => Task.FromResult(zip), sha: new string('0', 64)).ResolveAsync(() => { }));

        Assert.Equal("checksumMismatch", ex.Code);
        Assert.False(File.Exists(Path.Combine(_root, "data", "aria2", "aria2c.exe")));
    }

    [Fact]
    public async Task Windows_reports_download_failures()
    {
        var ex = await Assert.ThrowsAsync<Aria2LocateException>(() =>
            Locator(pathEnv: "", windows: true, download: _ => throw new HttpRequestException("offline")).ResolveAsync(() => { }));

        Assert.Equal("downloadFailed", ex.Code);
    }

    [Fact]
    public async Task Windows_skips_the_download_when_aria2_is_already_installed()
    {
        var installed = Touch("data", "aria2", "aria2c.exe");

        Assert.Equal(installed, await Locator(pathEnv: "", windows: true).ResolveAsync(() => { }));
    }
}
