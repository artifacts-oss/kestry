namespace Kestry.Server.Tests.Smoke;

/// <summary>A fact that is skipped when aria2c is not installed (CI installs it with apt).</summary>
public sealed class Aria2FactAttribute : FactAttribute
{
    public Aria2FactAttribute()
    {
        var path = Environment.GetEnvironmentVariable("PATH") ?? "";
        var found = path.Split(Path.PathSeparator).Any(d => File.Exists(Path.Combine(d, "aria2c")) || File.Exists(Path.Combine(d, "aria2c.exe")));
        if (!found) Skip = "aria2c is not installed";
    }
}
