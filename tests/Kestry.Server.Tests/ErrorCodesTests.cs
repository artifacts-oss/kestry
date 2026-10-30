using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Kestry.Server.Tests;

/// <summary>ErrorCodes is the single list of API error codes; the UI translations must match it exactly.</summary>
public partial class ErrorCodesTests
{
    private static readonly string Root = FindRoot();
    private static readonly string[] Locales = ["en", "es", "de", "fr"];

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Kestry.slnx"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Kestry.slnx not found above the test binaries");
    }

    private static string[] Codes() => typeof(ErrorCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToArray();

    /// <summary>Dotted paths of every string leaf under "Errors" in a locale file.</summary>
    private static List<string> TranslatedCodes(string locale)
    {
        var file = Path.Combine(Root, "src/kestry-web/src/i18n/messages", $"{locale}.json");
        using var doc = JsonDocument.Parse(File.ReadAllText(file));
        var leaves = new List<string>();
        void Walk(JsonElement node, string prefix)
        {
            foreach (var p in node.EnumerateObject())
            {
                var path = prefix.Length == 0 ? p.Name : $"{prefix}.{p.Name}";
                if (p.Value.ValueKind == JsonValueKind.Object) Walk(p.Value, path);
                else leaves.Add(path);
            }
        }
        Walk(doc.RootElement.GetProperty("Errors"), "");
        return leaves;
    }

    [Fact]
    public void Codes_are_unique_dotted_identifiers()
    {
        var codes = Codes();

        Assert.NotEmpty(codes);
        Assert.Equal(codes.Length, codes.Distinct().Count());
        Assert.All(codes, c => Assert.Matches(@"^[a-z][A-Za-z0-9]*(\.[a-z][A-Za-z0-9]*)+$", c));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("es")]
    [InlineData("de")]
    [InlineData("fr")]
    public void Every_code_is_translated_and_no_translation_is_orphaned(string locale)
    {
        var translated = TranslatedCodes(locale);

        Assert.Empty(Codes().Except(translated));      // add the missing "Errors" key to {locale}.json
        Assert.Empty(translated.Except(Codes()));      // delete it, or add the constant to ErrorCodes
    }

    [Fact]
    public void Covers_all_four_supported_locales() =>
        Assert.Equal(Locales.Order(), Directory.GetFiles(Path.Combine(Root, "src/kestry-web/src/i18n/messages"), "*.json")
            .Select(Path.GetFileNameWithoutExtension).Order());

    [GeneratedRegex(@"ErrorBody\(\s*""|new\(\s*""")]
    private static partial Regex LiteralCode();

    [Fact]
    public void Error_bodies_use_ErrorCodes_constants_never_string_literals()
    {
        // Aria2/ is skipped: `new("aria2.pause", …)` there builds RPC calls, not error bodies.
        var offenders = Directory.GetFiles(Path.Combine(Root, "src/Kestry.Server"), "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}Aria2{Path.DirectorySeparatorChar}"))
            .Where(f => LiteralCode().IsMatch(File.ReadAllText(f)))
            .Select(f => Path.GetRelativePath(Root, f));

        Assert.Empty(offenders);
    }
}
