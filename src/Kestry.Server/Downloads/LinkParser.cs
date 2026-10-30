using System.Text.RegularExpressions;

namespace Kestry.Server.Downloads;

public enum LinkKind { Http, Ftp, Magnet, TorrentUrl, Invalid }

public enum LinkState { Valid, Duplicate, Invalid }

/// <param name="Key">Identity used to detect duplicates: the normalized URL, or the lower-case hex info-hash for magnets.</param>
public sealed record ParsedLink(string Text, LinkKind Kind, LinkState State, string? Key);

/// <summary>Turns the pasted textarea into classified, de-duplicated links.</summary>
public static partial class LinkParser
{
    [GeneratedRegex(@"[\s,]+")]
    private static partial Regex Separators();

    [GeneratedRegex(@"(?:^|[?&])xt=urn:btih:([^&]+)", RegexOptions.IgnoreCase)]
    private static partial Regex InfoHash();

    /// <param name="existing">Keys of downloads aria2 already knows (see <see cref="KeyOf"/>).</param>
    public static List<ParsedLink> Parse(string text, IReadOnlySet<string>? existing = null)
    {
        var seen = new HashSet<string>();
        var result = new List<ParsedLink>();

        // Whole-line comments go first so a comma inside a comment cannot leak out as a "link".
        var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith('#'));
        foreach (var token in lines.SelectMany(l => Separators().Split(l)).Where(t => t.Length > 0))
        {
            var (kind, key) = Classify(token);
            var state = kind == LinkKind.Invalid ? LinkState.Invalid
                : !seen.Add(key!) || existing?.Contains(key!) == true ? LinkState.Duplicate
                : LinkState.Valid;
            result.Add(new ParsedLink(token, kind, state, key));
        }
        return result;
    }

    /// <summary>The duplicate-detection key for any URL or magnet, or null if it is not a supported link.</summary>
    public static string? KeyOf(string link) => Classify(link.Trim()).Key;

    private static (LinkKind Kind, string? Key) Classify(string token)
    {
        if (token.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
        {
            var hash = InfoHash().Match(token) is { Success: true } m ? NormalizeHash(Uri.UnescapeDataString(m.Groups[1].Value)) : null;
            return hash is null ? (LinkKind.Invalid, null) : (LinkKind.Magnet, "btih:" + hash);
        }

        if (!Uri.TryCreate(token, UriKind.Absolute, out var uri) || uri.Host.Length == 0) return (LinkKind.Invalid, null);
        var key = new UriBuilder(uri) { Fragment = "" }.Uri.AbsoluteUri;
        return uri.Scheme switch
        {
            "http" or "https" => (uri.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) ? LinkKind.TorrentUrl : LinkKind.Http, key),
            "ftp" => (uri.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase) ? LinkKind.TorrentUrl : LinkKind.Ftp, key),
            _ => (LinkKind.Invalid, null),
        };
    }

    /// <summary>Accepts a 40-char hex or 32-char base32 info-hash and returns it as lower-case hex.</summary>
    private static string? NormalizeHash(string hash)
    {
        if (hash.Length == 40 && hash.All(Uri.IsHexDigit)) return hash.ToLowerInvariant();
        if (hash.Length != 32) return null;

        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in hash.ToUpperInvariant())
        {
            var v = alphabet.IndexOf(c);
            if (v < 0) return null;
            buffer = (buffer << 5) | v;
            bits += 5;
            if (bits >= 8)
            {
                bits -= 8;
                bytes.Add((byte)((buffer >> bits) & 0xFF));
            }
        }
        return Convert.ToHexString([.. bytes]).ToLowerInvariant();
    }
}
