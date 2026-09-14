using NeteaseMusicCloudMatch.Core.Parsing;

namespace NeteaseMusicCloudMatch.Tests.Parsing;

public sealed class SongIdParserTests
{
    [Theory]
    [InlineData("186137", 186137L)]
    [InlineData("  186137  ", 186137L)]
    [InlineData("https://music.163.com/song?id=186137", 186137L)]
    [InlineData("https://music.163.com/#/song?id=186137", 186137L)]
    [InlineData("https://music.163.com/m/song?foo=bar&id=186137", 186137L)]
    [InlineData("https://y.music.163.com/m/song/186137", 186137L)]
    public void Parse_AcceptsSupportedSongInputs(string input, long expected)
    {
        var result = SongIdParser.Parse(input);

        Assert.True(result.Success);
        Assert.Equal(expected, result.SongId);
        Assert.Null(result.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("not-a-url")]
    [InlineData("https://example.com/song?id=186137")]
    [InlineData("https://music.163.com/playlist?id=186137")]
    [InlineData("https://music.163.com/#/album?id=186137")]
    [InlineData("https://music.163.com/toplist?id=186137")]
    [InlineData("https://music.163.com/song?id=abc")]
    public void Parse_RejectsAmbiguousOrInvalidInputs(string input)
    {
        var result = SongIdParser.Parse(input);

        Assert.False(result.Success);
        Assert.Null(result.SongId);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }
}
