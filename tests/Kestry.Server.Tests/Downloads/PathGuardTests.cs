using Kestry.Server.Downloads;

namespace Kestry.Server.Tests.Downloads;

public class PathGuardTests
{
    private static readonly string Root = Path.Combine(Path.GetTempPath(), "kestry-root");

    [Theory]
    [InlineData("a.bin", true)]
    [InlineData("sub/a.bin", true)]
    [InlineData("sub/../a.bin", true)]
    [InlineData("../a.bin", false)]
    [InlineData("sub/../../a.bin", false)]
    public void IsUnder_normalizes_dot_dot_segments(string relative, bool expected) =>
        Assert.Equal(expected, PathGuard.IsUnder(Path.Combine(Root, relative), Root));

    [Fact]
    public void A_sibling_with_the_same_prefix_is_not_inside()
    {
        Assert.False(PathGuard.IsUnder(Root + "-evil/a.bin", Root));
        Assert.True(PathGuard.IsUnder(Root, Root));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("movies", true)]
    [InlineData("movies/2024", true)]
    [InlineData("..", false)]
    [InlineData("../other", false)]
    [InlineData("/etc", false)]
    public void ResolveSubfolder_rejects_escapes(string? sub, bool ok) =>
        Assert.Equal(ok, PathGuard.ResolveSubfolder(Root, sub) is not null);
}
