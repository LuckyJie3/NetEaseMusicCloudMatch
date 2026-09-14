using System.Net;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NeteaseMusicCloudMatch.Core.Exceptions;
using NeteaseMusicCloudMatch.Infrastructure.Api;

namespace NeteaseMusicCloudMatch.Tests.Api;

public sealed class NeteaseApiClientTests
{
    private const string TestCookie = "MUSIC_U=unit-test-secret; __csrf=unit-test-csrf";

    [Fact]
    public async Task GetAccount_UsesPostAndParsesProfile()
    {
        const string json = """
            {"code":200,"profile":{"userId":12345,"nickname":"测试用户","avatarUrl":"https://p1.music.126.net/avatar.jpg"}}
            """;
        var (client, handler) = CreateClient(MockHttpMessageHandler.Json(json));

        var result = await client.GetAccountAsync(TestCookie);

        Assert.Equal(HttpMethod.Post, handler.Requests.Single().Method);
        Assert.Equal("/api/nuser/account/get", handler.Requests.Single().Uri?.AbsolutePath);
        Assert.Equal("application/x-www-form-urlencoded", handler.Requests.Single().ContentType);
        Assert.Equal(12345, result.Value?.UserId);
        Assert.Equal("测试用户", result.Value?.Nickname);
        Assert.Equal(200, result.ApiCode);
    }

    [Fact]
    public async Task GetAccount_AllowsMissingProfileForAuthLayerToReject()
    {
        var (client, _) = CreateClient(MockHttpMessageHandler.Json("{\"code\":200,\"profile\":null}"));

        var result = await client.GetAccountAsync(TestCookie);

        Assert.Null(result.Value);
    }

    [Fact]
    public async Task GetCloudSongs_UsesPostFormAndToleratesAbnormalItems()
    {
        const string json = """
            {
              "code": "200",
              "count": "2",
              "size": "1024",
              "maxSize": 4096,
              "data": [
                {
                  "songId": 111,
                  "fileName": "one.flac",
                  "fileSize": "2048",
                  "bitrate": 999000,
                  "addTime": 1700000000000,
                  "simpleSong": {
                    "id": 222,
                    "name": "歌曲一",
                    "ar": [{"name":"歌手甲"},{"name":"歌手乙"}],
                    "al": {"name":"专辑一","picUrl":"https://p1.music.126.net/cover.jpg"},
                    "dt": 123000
                  }
                },
                {"songId":{"unexpected":true},"fileSize":"not-a-number","simpleSong":"bad"}
              ]
            }
            """;
        var (client, handler) = CreateClient(MockHttpMessageHandler.Json(json));

        var result = await client.GetCloudSongsAsync(TestCookie, 200, 400);

        var request = handler.Requests.Single();
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/api/v1/cloud/get", request.Uri?.AbsolutePath);
        Assert.Equal("limit=200&offset=400", request.Body);
        Assert.Equal(2, result.Value.Songs.Count);
        Assert.Equal(111, result.Value.Songs[0].SongId);
        Assert.Equal(222, result.Value.Songs[0].CurrentMatchedSongId);
        Assert.Equal("歌手甲 / 歌手乙", result.Value.Songs[0].Artists);
        Assert.Null(result.Value.Songs[1].SongId);
        Assert.Null(result.Value.Songs[1].FileSize);
    }

    [Fact]
    public async Task GetCloudSongs_ToleratesMissingFields()
    {
        var (client, _) = CreateClient(MockHttpMessageHandler.Json("{\"code\":200}"));

        var result = await client.GetCloudSongsAsync(TestCookie, 200, 0);

        Assert.Empty(result.Value.Songs);
        Assert.Null(result.Value.TotalCount);
        Assert.Null(result.Value.UsedSize);
        Assert.Null(result.Value.MaxSize);
    }

    [Fact]
    public async Task GetSongDetail_ParsesLegacySongShape()
    {
        const string json = """
            {"code":200,"songs":[{"id":186137,"name":"双截棍","duration":210000,"artists":[{"name":"周杰伦"}],"album":{"name":"范特西","picUrl":"https://p1.music.126.net/cover.jpg"}}]}
            """;
        var (client, handler) = CreateClient(MockHttpMessageHandler.Json(json));

        var result = await client.GetSongDetailAsync(TestCookie, 186137);

        Assert.Equal(186137, result.Value?.SongId);
        Assert.Equal("周杰伦", result.Value?.Artists);
        Assert.Equal("范特西", result.Value?.Album);
        Assert.Contains("ids=%5B186137%5D", handler.Requests.Single().Uri?.Query, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Match_UsesCloudSongAsSongIdAndTargetAsAdjustSongId()
    {
        var (client, handler) = CreateClient(MockHttpMessageHandler.Json("{\"code\":200,\"matchData\":{\"songId\":222,\"fileName\":\"updated.flac\"}}"));

        var result = await client.MatchCloudSongAsync(TestCookie, 123, 222, 333);

        var request = handler.Requests.Single();
        var query = ParseQuery(request.Uri!);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.Equal("interface.music.163.com", request.Uri?.Host);
        Assert.Equal("123", query["userId"]);
        Assert.Equal("222", query["songId"]);
        Assert.Equal("333", query["adjustSongId"]);
        Assert.True(result.Value.Succeeded);
        Assert.Equal(222, result.Value.UpdatedSong?.SongId);
    }

    [Fact]
    public async Task Cancel_ProducesAdjustSongIdZero()
    {
        var (client, handler) = CreateClient(MockHttpMessageHandler.Json("{\"code\":200}"));

        await client.MatchCloudSongAsync(TestCookie, 123, 222, 0);

        var query = ParseQuery(handler.Requests.Single().Uri!);
        Assert.Equal("222", query["songId"]);
        Assert.Equal("0", query["adjustSongId"]);
    }

    [Fact]
    public async Task Code301_IsClassifiedAsCookieExpired()
    {
        var (client, _) = CreateClient(MockHttpMessageHandler.Json("{\"code\":301,\"message\":null}"));

        var exception = await Assert.ThrowsAsync<NeteaseApiException>(() => client.GetCloudSongsAsync(TestCookie, 200, 0));

        Assert.Equal(NeteaseErrorKind.CookieExpired, exception.Kind);
        Assert.DoesNotContain("unit-test-secret", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiError_DiagnosticMessageIsRedacted()
    {
        var (client, _) = CreateClient(MockHttpMessageHandler.Json("{\"code\":400,\"message\":\"MUSIC_U=server-leak; __csrf=csrf-leak\"}"));

        var exception = await Assert.ThrowsAsync<NeteaseApiException>(() => client.GetCloudSongsAsync(TestCookie, 200, 0));

        Assert.DoesNotContain("server-leak", exception.DiagnosticMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("csrf-leak", exception.DiagnosticMessage, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", exception.DiagnosticMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task WriteRequest_DoesNotRetryServerError()
    {
        var (client, handler) = CreateClient(
            MockHttpMessageHandler.Json("{\"code\":500,\"message\":\"temporary\"}", HttpStatusCode.ServiceUnavailable),
            MockHttpMessageHandler.Json("{\"code\":200}"));

        await Assert.ThrowsAsync<NeteaseApiException>(() => client.MatchCloudSongAsync(TestCookie, 1, 2, 3));

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Match_UnsupportedCloudSongHasSpecificClassification()
    {
        var (client, _) = CreateClient(MockHttpMessageHandler.Json("{\"code\":400,\"message\":\"当前歌曲不支持匹配\"}"));

        var exception = await Assert.ThrowsAsync<NeteaseApiException>(() =>
            client.MatchCloudSongAsync(TestCookie, 1, 2, 3));

        Assert.Equal(NeteaseErrorKind.CloudSongNotMatchable, exception.Kind);
        Assert.Contains("不允许纠正这首云盘歌曲", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SuccessfulRequest_LogsSanitizedResponseJsonWithoutCredentials()
    {
        const string json = """
            {"code":200,"profile":{"userId":12345},"MUSIC_U":"server-secret","message":"__csrf=csrf-secret"}
            """;
        var handler = new MockHttpMessageHandler(MockHttpMessageHandler.Json(json));
        var logger = new RecordingLogger<NeteaseApiClient>();
        var client = CreateClient(handler, logger);

        await client.GetAccountAsync(TestCookie);

        var message = Assert.Single(logger.Messages, value => value.StartsWith("Netease request completed.", StringComparison.Ordinal));
        Assert.Contains("ResponseJson=", message, StringComparison.Ordinal);
        Assert.Contains("[REDACTED]", message, StringComparison.Ordinal);
        Assert.DoesNotContain("server-secret", message, StringComparison.Ordinal);
        Assert.DoesNotContain("csrf-secret", message, StringComparison.Ordinal);
        Assert.DoesNotContain("unit-test-secret", message, StringComparison.Ordinal);
    }

    private static (NeteaseApiClient Client, MockHttpMessageHandler Handler) CreateClient(
        params Func<HttpRequestMessage, HttpResponseMessage>[] responses)
    {
        var handler = new MockHttpMessageHandler(responses);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new NeteaseApiOptions
        {
            BaseUri = new Uri("https://music.163.com"),
            Timeout = TimeSpan.FromSeconds(5),
            MaximumReadAttempts = 1
        });
        var client = new NeteaseApiClient(httpClient, options, NullLogger<NeteaseApiClient>.Instance);
        return (client, handler);
    }

    private static NeteaseApiClient CreateClient(
        MockHttpMessageHandler handler,
        ILogger<NeteaseApiClient> logger)
    {
        var httpClient = new HttpClient(handler);
        var options = Options.Create(new NeteaseApiOptions
        {
            BaseUri = new Uri("https://music.163.com"),
            Timeout = TimeSpan.FromSeconds(5),
            MaximumReadAttempts = 1
        });
        return new NeteaseApiClient(httpClient, options, logger);
    }

    private static Dictionary<string, string> ParseQuery(Uri uri) =>
        uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .ToDictionary(
                pair => Uri.UnescapeDataString(pair[0]),
                pair => pair.Length == 2 ? Uri.UnescapeDataString(pair[1]) : string.Empty,
                StringComparer.OrdinalIgnoreCase);

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
