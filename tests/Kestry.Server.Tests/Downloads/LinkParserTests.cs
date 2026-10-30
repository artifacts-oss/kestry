using Kestry.Server.Downloads;

namespace Kestry.Server.Tests.Downloads;

public class LinkParserTests
{
    private const string Hex = "c12fe1c06bba254a9dc9f519b335aa7c1367a88a";
    private static readonly string Magnet = $"magnet:?xt=urn:btih:{Hex}&dn=ubuntu";

    private static ParsedLink One(string text) => Assert.Single(LinkParser.Parse(text));

    [Theory]
    [InlineData("http://example.com/a.zip", LinkKind.Http)]
    [InlineData("https://example.com/a.zip?x=1", LinkKind.Http)]
    [InlineData("ftp://example.com/pub/a.iso", LinkKind.Ftp)]
    [InlineData("https://example.com/ubuntu.torrent", LinkKind.TorrentUrl)]
    [InlineData("http://example.com/ubuntu.TORRENT", LinkKind.TorrentUrl)]
    [InlineData("magnet:?xt=urn:btih:c12fe1c06bba254a9dc9f519b335aa7c1367a88a&dn=x", LinkKind.Magnet)]
    public void Classifies_supported_links(string link, LinkKind kind)
    {
        var parsed = One(link);

        Assert.Equal(kind, parsed.Kind);
        Assert.Equal(LinkState.Valid, parsed.State);
    }

    [Theory]
    [InlineData("not a link")]
    [InlineData("example.com/a.zip")]
    [InlineData("file:///etc/passwd")]
    [InlineData("sftp://host/file")]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://")]
    [InlineData("magnet:?dn=no-hash")]
    [InlineData("magnet:?xt=urn:btih:tooshort")]
    [InlineData("magnet:?xt=urn:sha1:c12fe1c06bba254a9dc9f519b335aa7c1367a88a")]
    public void Rejects_unsupported_or_malformed_links(string link)
    {
        var parsed = LinkParser.Parse(link).First();

        Assert.Equal(LinkState.Invalid, parsed.State);
        Assert.Equal(LinkKind.Invalid, parsed.Kind);
    }

    [Fact]
    public void Splits_on_newlines_spaces_and_commas_and_trims()
    {
        var links = LinkParser.Parse("  http://a.com/1 ,http://a.com/2\r\nhttp://a.com/3\t http://a.com/4  \n\n");

        Assert.Equal(["http://a.com/1", "http://a.com/2", "http://a.com/3", "http://a.com/4"], links.Select(l => l.Text));
    }

    [Fact]
    public void Ignores_empty_input_and_comment_lines()
    {
        Assert.Empty(LinkParser.Parse(""));
        Assert.Empty(LinkParser.Parse("   \n\n"));
        var links = LinkParser.Parse("# my list, with a comma\nhttp://a.com/1\n  # indented comment\nhttp://a.com/2");
        Assert.Equal(["http://a.com/1", "http://a.com/2"], links.Select(l => l.Text));
    }

    [Fact]
    public void Marks_repeated_links_in_the_same_batch_as_duplicates()
    {
        var links = LinkParser.Parse("http://a.com/f\nhttp://a.com/f\nhttp://A.com/f#frag");

        Assert.Equal([LinkState.Valid, LinkState.Duplicate, LinkState.Duplicate], links.Select(l => l.State));
    }

    [Fact]
    public void Magnets_are_duplicates_by_info_hash_regardless_of_case_params_and_encoding()
    {
        var links = LinkParser.Parse($"{Magnet}\nmagnet:?dn=other&xt=urn:btih:{Hex.ToUpperInvariant()}&tr=udp%3A%2F%2Ft");

        Assert.Equal([LinkState.Valid, LinkState.Duplicate], links.Select(l => l.State));
    }

    [Fact]
    public void Base32_and_hex_info_hashes_are_the_same_download()
    {
        // 20 bytes 0x00..0x13 as hex and as RFC 4648 base32.
        const string hex = "000102030405060708090a0b0c0d0e0f10111213";
        const string base32 = "AAAQEAYEAUDAOCAJBIFQYDIOB4IBCEQT";

        var links = LinkParser.Parse($"magnet:?xt=urn:btih:{hex}\nmagnet:?xt=urn:btih:{base32}");

        Assert.Equal([LinkState.Valid, LinkState.Duplicate], links.Select(l => l.State));
        Assert.Equal("btih:" + hex, links[0].Key);
        Assert.Equal(links[0].Key, links[1].Key);
    }

    [Fact]
    public void Marks_links_that_already_exist_as_duplicates()
    {
        var existing = new HashSet<string> { LinkParser.KeyOf("http://a.com/old")!, "btih:" + Hex };

        var links = LinkParser.Parse($"http://a.com/old\nhttp://a.com/new\n{Magnet}", existing);

        Assert.Equal([LinkState.Duplicate, LinkState.Valid, LinkState.Duplicate], links.Select(l => l.State));
    }

    [Fact]
    public void Invalid_entries_never_count_as_duplicates_of_each_other()
    {
        var links = LinkParser.Parse("junk junk");

        Assert.All(links, l => Assert.Equal(LinkState.Invalid, l.State));
    }

    [Fact]
    public void KeyOf_returns_null_for_unsupported_text() => Assert.Null(LinkParser.KeyOf("hello"));
}
