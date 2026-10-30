namespace Kestry.Server.Downloads;

/// <summary>Keeps every filesystem write/delete inside the configured roots.</summary>
public static class PathGuard
{
    private static StringComparison Comparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    public static bool IsUnder(string path, string root)
    {
        var full = Path.GetFullPath(path);
        var rootFull = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        return full.Equals(rootFull, Comparison) || full.StartsWith(rootFull + Path.DirectorySeparatorChar, Comparison);
    }

    public static bool IsUnderAny(string path, IEnumerable<string> roots) => roots.Any(r => IsUnder(path, r));

    /// <summary>Joins a user-supplied relative subfolder to <paramref name="baseDir"/>; null if it would escape it.</summary>
    public static string? ResolveSubfolder(string baseDir, string? subfolder)
    {
        if (string.IsNullOrWhiteSpace(subfolder)) return baseDir;
        if (Path.IsPathRooted(subfolder)) return null;
        var combined = Path.GetFullPath(Path.Combine(baseDir, subfolder));
        return IsUnder(combined, baseDir) ? combined : null;
    }
}
