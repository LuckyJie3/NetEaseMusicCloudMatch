using NeteaseMusicCloudMatch.Core.Security;

namespace NeteaseMusicCloudMatch.Tests.Security;

public sealed class CookieParserTests
{
    [Fact]
    public void Parse_PreservesCompleteCookieAndValuesContainingEquals()
    {
        var result = CookieParser.Parse("MUSIC_U=abc==; __csrf=token; theme=dark");

        Assert.Equal("MUSIC_U=abc==; __csrf=token; theme=dark", result.HeaderValue);
        Assert.Equal("abc==", result.Values["MUSIC_U"]);
        Assert.Equal("token", result.Values["__csrf"]);
    }

    [Fact]
    public void Parse_AcceptsCopiedHeaderPrefix()
    {
        var result = CookieParser.Parse("Cookie: MUSIC_U=secret; os=pc");

        Assert.Equal("MUSIC_U=secret; os=pc", result.HeaderValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("only-a-name")]
    [InlineData("MUSIC_U=value\r\nAuthorization: injected")]
    public void Parse_RejectsInvalidCookie(string? input)
    {
        Assert.Throws<FormatException>(() => CookieParser.Parse(input));
    }
}
