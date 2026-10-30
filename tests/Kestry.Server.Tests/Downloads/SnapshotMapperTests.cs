using Kestry.Server.Aria2;
using Kestry.Server.Downloads;

namespace Kestry.Server.Tests.Downloads;

public class SnapshotMapperTests
{
    private static Aria2Status St(string gid, string status, string? path = null, string? uri = null, long total = 0, long done = 0, Action<Aria2Status>? _ = null) => new()
    {
        Gid = gid,
        Status = status,
        TotalLength = total,
        CompletedLength = done,
        Dir = "/d",
        Files = [new Aria2File { Path = path ?? "", Uris = uri is null ? [] : [new Aria2Uri { Uri = uri }] }],
    };

    private static DownloadDto One(Aria2Status s) => SnapshotMapper.Map([s], [], []).Single();

    [Theory]
    [InlineData("active", "downloading")]
    [InlineData("waiting", "queued")]
    [InlineData("paused", "paused")]
    [InlineData("complete", "completed")]
    [InlineData("error", "failed")]
    public void Maps_aria2_states_to_kestrel_states(string aria2, string expected) =>
        Assert.Equal(expected, One(St("g", aria2, "/d/a.bin")).Status);

    [Fact]
    public void Removed_downloads_are_hidden() =>
        Assert.Empty(SnapshotMapper.Map([], [], [St("g", "removed", "/d/a")]));

    [Theory]
    [InlineData("http://x/a.bin", "http")]
    [InlineData("https://x/a.bin", "https")]
    [InlineData("ftp://x/a.bin", "ftp")]
    public void Source_comes_from_the_uri_scheme(string uri, string source) =>
        Assert.Equal(source, One(St("g", "active", "/d/a.bin", uri)).Source);

    [Fact]
    public void Computes_eta_from_remaining_bytes_and_speed()
    {
        var s = St("g", "active", "/d/a.bin", total: 1000, done: 400) with { DownloadSpeed = 100 };
        Assert.Equal(6, One(s).Eta);
    }

    [Fact]
    public void Eta_is_unknown_without_speed() => Assert.Null(One(St("g", "active", "/d/a", total: 10)).Eta);

    [Theory]
    [InlineData("movie.MKV", "video")]
    [InlineData("a.zip", "archive")]
    [InlineData("song.flac", "music")]
    [InlineData("pic.png", "image")]
    [InlineData("readme", "file")]
    public void File_type_comes_from_the_extension(string name, string type) =>
        Assert.Equal(type, One(St("g", "complete", "/d/" + name)).FileType);

    [Fact]
    public void Name_is_the_file_name_and_path_the_full_path()
    {
        var dto = One(St("g", "active", "/d/sub/a.bin"));
        Assert.Equal("a.bin", dto.Name);
        Assert.Equal("/d/sub/a.bin", dto.Path);
    }

    [Fact]
    public void Name_falls_back_to_the_uri_before_aria2_knows_the_file()
    {
        Assert.Equal("a.bin", One(St("g", "waiting", uri: "http://x/files/a.bin")).Name);
    }

    [Fact]
    public void Failed_downloads_carry_the_aria2_error()
    {
        var s = St("g", "error", "/d/a") with { ErrorMessage = "File not found" };
        Assert.Equal("File not found", One(s).Error);
    }

    [Fact]
    public void Multi_file_torrents_use_the_torrent_name_and_folder()
    {
        var s = new Aria2Status
        {
            Gid = "t", Status = "active", InfoHash = "abc", Dir = "/d", TotalLength = 10,
            BitTorrent = new Aria2BitTorrent { Info = new Aria2BtInfo { Name = "Album" } },
            Files = [new Aria2File { Path = "/d/Album/1.mp3" }, new Aria2File { Path = "/d/Album/2.mp3" }],
        };

        var dto = One(s);

        Assert.Equal("Album", dto.Name);
        Assert.Equal(Path.Combine("/d", "Album"), dto.Path);
        Assert.Equal("torrent", dto.Source);
    }

    [Fact]
    public void A_magnet_shows_one_row_the_successor_and_labels_it_magnet()
    {
        var metadata = St("m", "complete", "/d/[METADATA]abc", "magnet:?xt=urn:btih:abc") with { FollowedBy = ["t"], InfoHash = "abc" };
        var real = new Aria2Status
        {
            Gid = "t", Status = "active", InfoHash = "abc", Following = "m", TotalLength = 100, CompletedLength = 50, Dir = "/d",
            BitTorrent = new Aria2BitTorrent { Info = new Aria2BtInfo { Name = "ubuntu.iso" } },
            Files = [new Aria2File { Path = "/d/ubuntu.iso" }],
        };

        var list = SnapshotMapper.Map([real], [], [metadata]);

        var row = Assert.Single(list);
        Assert.Equal("t", row.Gid);
        Assert.Equal("magnet", row.Source);
        Assert.False(row.FetchingMetadata);
    }

    [Fact]
    public void A_torrent_url_successor_is_labelled_torrent_not_magnet()
    {
        var torrentFile = St("m", "complete", "/d/a.torrent", "http://x/a.torrent") with { FollowedBy = ["t"] };
        var real = new Aria2Status { Gid = "t", Status = "active", InfoHash = "abc", Following = "m", Files = [new Aria2File { Path = "/d/a" }] };

        Assert.Equal("torrent", SnapshotMapper.Map([real], [], [torrentFile]).Single().Source);
    }

    [Fact]
    public void A_magnet_without_metadata_is_flagged_and_named_after_its_hash()
    {
        var s = new Aria2Status
        {
            Gid = "m", Status = "active", InfoHash = "abc123", Dir = "/d",
            Files = [new Aria2File { Path = "[METADATA]abc123", Uris = [new Aria2Uri { Uri = "magnet:?xt=urn:btih:abc123" }] }],
        };

        var dto = One(s);

        Assert.True(dto.FetchingMetadata);
        Assert.Equal("abc123", dto.Name);
        Assert.Equal("torrent", dto.Source); // refined to "magnet" once it has a successor
    }

    [Fact]
    public void A_fully_downloaded_torrent_that_is_still_uploading_counts_as_completed_and_seeding()
    {
        var s = new Aria2Status
        {
            Gid = "t", Status = "active", InfoHash = "abc", TotalLength = 10, CompletedLength = 10,
            Files = [new Aria2File { Path = "/d/a" }],
        };

        var dto = One(s);

        Assert.Equal("completed", dto.Status);
        Assert.True(dto.Seeding);
    }

    [Fact]
    public void Orders_active_then_waiting_then_stopped()
    {
        var list = SnapshotMapper.Map([St("a", "active", "/d/a")], [St("w", "waiting", "/d/w")], [St("s", "complete", "/d/s")]);

        Assert.Equal(["a", "w", "s"], list.Select(d => d.Gid));
    }
}
