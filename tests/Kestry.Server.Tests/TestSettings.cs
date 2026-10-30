using Kestry.Server.Aria2;
using Kestry.Server.Settings;
using Microsoft.Extensions.Options;

namespace Kestry.Server.Tests;

public static class TestSettings
{
    public static IOptions<KestryOptions> Options(string root) =>
        Microsoft.Extensions.Options.Options.Create(new KestryOptions { DataDir = Path.Combine(root, "data"), AllowedRoots = [Path.Combine(root, "downloads")] });

    public static SettingsService Create(string root, Aria2Client? client = null, Aria2Runtime? runtime = null) =>
        new(Options(root), client ?? new Aria2.FakeAria2().Client, runtime ?? new Aria2Runtime());
}
