using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NeteaseMusicCloudMatch.Core.Exceptions;
using NeteaseMusicCloudMatch.Core.Models;
using NeteaseMusicCloudMatch.Core.Security;
using NeteaseMusicCloudMatch.Core.Services;

namespace NeteaseMusicCloudMatch.Infrastructure.Api;

public sealed class NeteaseApiClient : INeteaseApiClient
{
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMilliseconds(300),
        TimeSpan.FromMilliseconds(900)
    ];

    private readonly HttpClient _httpClient;
    private readonly NeteaseApiOptions _options;
    private readonly ILogger<NeteaseApiClient> _logger;

    public NeteaseApiClient(
        HttpClient httpClient,
        IOptions<NeteaseApiOptions> options,
        ILogger<NeteaseApiClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;

        ValidateBaseUri(_options);
        _httpClient.BaseAddress ??= _options.BaseUri;
        _httpClient.Timeout = _options.Timeout;
    }

    public Task<ApiCallResult<UserProfile?>> GetAccountAsync(
        string cookie,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            NeteaseEndpoints.Account,
            cookie,
            query: null,
            form: null,
            ParseAccount,
            cancellationToken);

    public Task<ApiCallResult<CloudPage>> GetCloudSongsAsync(
        string cookie,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);

        return SendAsync(
            NeteaseEndpoints.Cloud,
            cookie,
            query: null,
            form: new Dictionary<string, string>
            {
                ["limit"] = limit.ToString(CultureInfo.InvariantCulture),
                ["offset"] = offset.ToString(CultureInfo.InvariantCulture)
            },
            root => ParseCloudPage(root, limit, offset),
            cancellationToken);
    }

    public Task<ApiCallResult<CloudSong?>> GetCloudSongByIdAsync(
        string cookie,
        long cloudSongId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cloudSongId);
        return SendAsync(
            NeteaseEndpoints.CloudByIds,
            cookie,
            query: new Dictionary<string, string>
            {
                ["songIds"] = $"[{cloudSongId.ToString(CultureInfo.InvariantCulture)}]"
            },
            form: null,
            root => ParseCloudSongs(root).FirstOrDefault(),
            cancellationToken);
    }

    public Task<ApiCallResult<SongDetails?>> GetSongDetailAsync(
        string cookie,
        long targetSongId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSongId);
        return SendAsync(
            NeteaseEndpoints.SongDetail,
            cookie,
            query: new Dictionary<string, string>
            {
                ["ids"] = $"[{targetSongId.ToString(CultureInfo.InvariantCulture)}]"
            },
            form: null,
            root => ParseSongDetails(root, targetSongId),
            cancellationToken);
    }

    public Task<ApiCallResult<MatchResult>> MatchCloudSongAsync(
        string cookie,
        long userId,
        long cloudSongId,
        long targetSongId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(userId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(cloudSongId);
        ArgumentOutOfRangeException.ThrowIfNegative(targetSongId);

        return SendAsync(
            NeteaseEndpoints.CloudMatch,
            cookie,
            query: new Dictionary<string, string>
            {
                ["userId"] = userId.ToString(CultureInfo.InvariantCulture),
                ["songId"] = cloudSongId.ToString(CultureInfo.InvariantCulture),
                ["adjustSongId"] = targetSongId.ToString(CultureInfo.InvariantCulture)
            },
            form: null,
            root => new MatchResult(true, ParseMatchData(root)),
            cancellationToken);
    }

    private async Task<ApiCallResult<T>> SendAsync<T>(
        NeteaseEndpoint endpoint,
        string cookie,
        IReadOnlyDictionary<string, string>? query,
        IReadOnlyDictionary<string, string>? form,
        Func<JsonElement, T> parse,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(cookie);
        var correlationId = Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
        var stopwatch = Stopwatch.StartNew();
        var attempts = endpoint.IsWriteOperation ? 1 : Math.Clamp(_options.MaximumReadAttempts, 1, 3);
        string? responseDiagnostic = null;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            responseDiagnostic = null;
            try
            {
                using var request = CreateRequest(endpoint, cookie, query, form);
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    cancellationToken).ConfigureAwait(false);

                if (ShouldRetry(response.StatusCode, endpoint, attempt, attempts))
                {
                    var delay = GetRetryDelay(response.Headers.RetryAfter, attempt);
                    _logger.LogWarning(
                        "Netease read request will retry. Operation={Operation} Method={Method} Host={Host} Endpoint={Endpoint} HttpStatus={HttpStatus} Attempt={Attempt} CorrelationId={CorrelationId}",
                        endpoint.Name,
                        endpoint.Method.Method,
                        request.RequestUri?.Host,
                        endpoint.Path,
                        (int)response.StatusCode,
                        attempt,
                        correlationId);
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                responseDiagnostic = DiagnosticSanitizer.SanitizeJson(responseBody);
                JsonDocument? document = null;
                try
                {
                    document = JsonDocument.Parse(responseBody);
                }
                catch (JsonException jsonException)
                {
                    throw CreateException(
                        NeteaseErrorKind.UnknownApiError,
                        "服务器返回了无法识别的数据。",
                        endpoint,
                        correlationId,
                        response.StatusCode,
                        null,
                        "Invalid JSON response",
                        jsonException);
                }

                using (document)
                {
                    var root = document.RootElement;
                    var apiCode = root.ValueKind == JsonValueKind.Object ? root.GetFlexibleInt32("code") : null;
                    EnsureSuccess(response.StatusCode, apiCode, root, endpoint, correlationId);
                    var value = parse(root);
                    stopwatch.Stop();

                    _logger.LogInformation(
                        "Netease request completed. Operation={Operation} Method={Method} Host={Host} Endpoint={Endpoint} HttpStatus={HttpStatus} ApiCode={ApiCode} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId} ResponseJson={ResponseJson:l}",
                        endpoint.Name,
                        endpoint.Method.Method,
                        request.RequestUri?.Host,
                        endpoint.Path,
                        (int)response.StatusCode,
                        apiCode,
                        stopwatch.ElapsedMilliseconds,
                        correlationId,
                        responseDiagnostic);

                    return new ApiCallResult<T>(
                        value,
                        response.StatusCode,
                        apiCode,
                        stopwatch.Elapsed,
                        correlationId);
                }
            }
            catch (NeteaseApiException exception)
            {
                stopwatch.Stop();
                LogRequestFailure(endpoint, exception, stopwatch.ElapsedMilliseconds, correlationId, responseDiagnostic);
                throw;
            }
            catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                var wrapped = CreateException(
                    NeteaseErrorKind.Timeout,
                    endpoint.IsWriteOperation
                        ? "请求超时，匹配结果可能不确定。请先刷新云盘确认当前状态。"
                        : "请求超时，请稍后重试。",
                    endpoint,
                    correlationId,
                    diagnosticMessage: "HTTP request timed out",
                    innerException: exception);
                LogRequestFailure(endpoint, wrapped, stopwatch.ElapsedMilliseconds, correlationId, responseDiagnostic);
                throw wrapped;
            }
            catch (HttpRequestException exception) when (!endpoint.IsWriteOperation && attempt < attempts)
            {
                _logger.LogWarning(
                    "Netease read request failed transiently and will retry. Operation={Operation} Endpoint={Endpoint} Attempt={Attempt} ErrorType={ErrorType} CorrelationId={CorrelationId}",
                    endpoint.Name,
                    endpoint.Path,
                    attempt,
                    exception.GetType().Name,
                    correlationId);
                await Task.Delay(RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)], cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException exception)
            {
                stopwatch.Stop();
                var wrapped = CreateException(
                    NeteaseErrorKind.NetworkError,
                    endpoint.IsWriteOperation
                        ? "网络连接中断，结果可能不确定。请先刷新云盘确认当前状态。"
                        : "网络连接失败，请检查网络后重试。",
                    endpoint,
                    correlationId,
                    diagnosticMessage: exception.GetType().Name,
                    innerException: exception);
                LogRequestFailure(endpoint, wrapped, stopwatch.ElapsedMilliseconds, correlationId, responseDiagnostic);
                throw wrapped;
            }
        }

        stopwatch.Stop();
        var exhausted = CreateException(
            NeteaseErrorKind.NetworkError,
            "网络连接失败，请检查网络后重试。",
            endpoint,
            correlationId,
            diagnosticMessage: "Read retry attempts exhausted");
        LogRequestFailure(endpoint, exhausted, stopwatch.ElapsedMilliseconds, correlationId, responseDiagnostic);
        throw exhausted;
    }

    private HttpRequestMessage CreateRequest(
        NeteaseEndpoint endpoint,
        string cookie,
        IReadOnlyDictionary<string, string>? query,
        IReadOnlyDictionary<string, string>? form)
    {
        var requestUri = new Uri(GetBaseUri(endpoint), BuildRelativeUri(endpoint.Path, query));
        var request = new HttpRequestMessage(endpoint.Method, requestUri);
        request.Headers.TryAddWithoutValidation("Cookie", cookie);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.UserAgent.ParseAdd("NeteaseMusicCloudMatch/1.0");
        if (form is not null || endpoint.Method == HttpMethod.Post)
        {
            request.Content = new FormUrlEncodedContent(form ?? new Dictionary<string, string>());
        }

        return request;
    }

    private Uri GetBaseUri(NeteaseEndpoint endpoint) =>
        endpoint.Host == NeteaseApiHost.Interface ? _options.InterfaceBaseUri : _options.BaseUri;

    private static string BuildRelativeUri(string path, IReadOnlyDictionary<string, string>? query)
    {
        if (query is null || query.Count == 0)
        {
            return path;
        }

        var queryString = string.Join("&", query.Select(pair =>
            $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));
        return $"{path}?{queryString}";
    }

    private static UserProfile? ParseAccount(JsonElement root)
    {
        if (!root.TryGetProperty("profile", out var profile) || profile.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var userId = profile.GetFlexibleInt64("userId");
        if (userId is not > 0)
        {
            return null;
        }

        return new UserProfile(
            userId.Value,
            profile.GetFlexibleString("nickname"),
            profile.GetUriOrNull("avatarUrl"));
    }

    private static CloudPage ParseCloudPage(JsonElement root, int limit, int offset) =>
        new(
            root.GetFlexibleInt32("count"),
            root.GetFlexibleInt64("size"),
            root.GetFlexibleInt64("maxSize"),
            ParseCloudSongs(root),
            limit,
            offset);

    private static IReadOnlyList<CloudSong> ParseCloudSongs(JsonElement root)
    {
        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var songs = new List<CloudSong>();
        var index = 0;
        foreach (var item in data.EnumerateArray())
        {
            songs.Add(ParseCloudSong(item, index++));
        }

        return songs;
    }

    private static CloudSong ParseCloudSong(JsonElement item, int sourceIndex)
    {
        if (item.ValueKind != JsonValueKind.Object)
        {
            return new CloudSong(sourceIndex, null, null, null, null, null, null, null, null, null, null, null);
        }

        var hasSimpleSong = item.TryGetProperty("simpleSong", out var simpleSong) && simpleSong.ValueKind == JsonValueKind.Object;
        var metadata = hasSimpleSong ? simpleSong : item;
        var album = GetNestedObject(metadata, "al") ?? GetNestedObject(metadata, "album");

        return new CloudSong(
            sourceIndex,
            item.GetFlexibleInt64("songId"),
            hasSimpleSong ? simpleSong.GetFlexibleInt64("id") : null,
            metadata.GetFlexibleString("name") ?? item.GetFlexibleString("songName"),
            GetArtists(metadata) ?? item.GetFlexibleString("artist"),
            album?.GetFlexibleString("name") ?? item.GetFlexibleString("album"),
            item.GetFlexibleString("fileName"),
            item.GetFlexibleInt64("fileSize"),
            item.GetFlexibleInt32("bitrate"),
            ParseUnixTime(item.GetFlexibleInt64("addTime")),
            album?.GetUriOrNull("picUrl"),
            metadata.GetFlexibleInt64("dt"));
    }

    private static SongDetails? ParseSongDetails(JsonElement root, long requestedId)
    {
        if (!root.TryGetProperty("songs", out var songs) || songs.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var song in songs.EnumerateArray())
        {
            if (song.ValueKind != JsonValueKind.Object || song.GetFlexibleInt64("id") is not { } id || id != requestedId)
            {
                continue;
            }

            var album = GetNestedObject(song, "album") ?? GetNestedObject(song, "al");
            return new SongDetails(
                id,
                song.GetFlexibleString("name"),
                GetArtists(song),
                album?.GetFlexibleString("name"),
                album?.GetUriOrNull("picUrl"),
                song.GetFlexibleInt64("duration") ?? song.GetFlexibleInt64("dt"));
        }

        return null;
    }

    private static CloudSong? ParseMatchData(JsonElement root)
    {
        if (!root.TryGetProperty("matchData", out var matchData) || matchData.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        return ParseCloudSong(matchData, 0);
    }

    private static JsonElement? GetNestedObject(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Object ? value : null;

    private static string? GetArtists(JsonElement element)
    {
        JsonElement artists;
        if (!(element.TryGetProperty("artists", out artists) || element.TryGetProperty("ar", out artists)) ||
            artists.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var names = artists.EnumerateArray()
            .Where(artist => artist.ValueKind == JsonValueKind.Object)
            .Select(artist => artist.GetFlexibleString("name"))
            .Where(name => !string.IsNullOrWhiteSpace(name));
        var result = string.Join(" / ", names!);
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static DateTimeOffset? ParseUnixTime(long? value)
    {
        if (value is null or <= 0)
        {
            return null;
        }

        try
        {
            return value > 10_000_000_000
                ? DateTimeOffset.FromUnixTimeMilliseconds(value.Value)
                : DateTimeOffset.FromUnixTimeSeconds(value.Value);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private static bool ShouldRetry(HttpStatusCode statusCode, NeteaseEndpoint endpoint, int attempt, int attempts) =>
        !endpoint.IsWriteOperation &&
        attempt < attempts &&
        (statusCode == HttpStatusCode.TooManyRequests ||
         statusCode == HttpStatusCode.BadGateway ||
         statusCode == HttpStatusCode.ServiceUnavailable ||
         statusCode == HttpStatusCode.GatewayTimeout);

    private static TimeSpan GetRetryDelay(RetryConditionHeaderValue? retryAfter, int attempt)
    {
        if (retryAfter?.Delta is { } delta && delta > TimeSpan.Zero && delta <= TimeSpan.FromSeconds(10))
        {
            return delta;
        }

        return RetryDelays[Math.Min(attempt - 1, RetryDelays.Length - 1)];
    }

    private static void EnsureSuccess(
        HttpStatusCode httpStatus,
        int? apiCode,
        JsonElement root,
        NeteaseEndpoint endpoint,
        string correlationId)
    {
        var rawMessage = root.ValueKind == JsonValueKind.Object
            ? root.GetFlexibleString("message") ?? root.GetFlexibleString("msg")
            : null;
        var diagnosticMessage = Truncate(DiagnosticSanitizer.Redact(rawMessage));

        if (!IsHttpSuccess(httpStatus))
        {
            var kind = httpStatus switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => NeteaseErrorKind.Unauthorized,
                HttpStatusCode.TooManyRequests => NeteaseErrorKind.RateLimited,
                >= HttpStatusCode.InternalServerError => NeteaseErrorKind.ServerError,
                _ => NeteaseErrorKind.UnknownApiError
            };
            throw CreateException(kind, GetUserMessage(kind, endpoint.IsWriteOperation), endpoint, correlationId, httpStatus, apiCode, diagnosticMessage);
        }

        if (apiCode == 200)
        {
            return;
        }

        var apiKind = apiCode switch
        {
            400 when endpoint == NeteaseEndpoints.CloudMatch &&
                     rawMessage?.Contains("当前歌曲不支持匹配", StringComparison.Ordinal) == true =>
                NeteaseErrorKind.CloudSongNotMatchable,
            301 => NeteaseErrorKind.CookieExpired,
            401 or 403 => NeteaseErrorKind.Unauthorized,
            429 => NeteaseErrorKind.RateLimited,
            >= 500 => NeteaseErrorKind.ServerError,
            _ => NeteaseErrorKind.UnknownApiError
        };
        throw CreateException(apiKind, GetUserMessage(apiKind, endpoint.IsWriteOperation), endpoint, correlationId, httpStatus, apiCode, diagnosticMessage);
    }

    private static bool IsHttpSuccess(HttpStatusCode statusCode) => (int)statusCode is >= 200 and <= 299;

    private static string GetUserMessage(NeteaseErrorKind kind, bool isWriteOperation) => kind switch
    {
        NeteaseErrorKind.Unauthorized or NeteaseErrorKind.CookieExpired =>
            "Cookie 无效或已过期，请重新登录网易云网页版后复制新的 Cookie。",
        NeteaseErrorKind.RateLimited => "请求过于频繁，请稍后再试。",
        NeteaseErrorKind.CloudSongNotMatchable =>
            "网易云不允许纠正这首云盘歌曲。它可能不是可手动纠正的上传条目，或受到当前关联状态、曲库可用性等服务端限制。请换一首云盘歌曲验证。",
        NeteaseErrorKind.ServerError when isWriteOperation =>
            "服务器响应异常，匹配结果可能不确定。请先刷新云盘确认当前状态。",
        NeteaseErrorKind.ServerError => "网易云服务暂时异常，请稍后重试。",
        _ => "网易云接口返回了未识别的错误。"
    };

    private static NeteaseApiException CreateException(
        NeteaseErrorKind kind,
        string userMessage,
        NeteaseEndpoint endpoint,
        string correlationId,
        HttpStatusCode? httpStatus = null,
        int? apiCode = null,
        string? diagnosticMessage = null,
        Exception? innerException = null) =>
        new(
            kind,
            userMessage,
            endpoint.Name,
            correlationId,
            httpStatus,
            apiCode,
            DiagnosticSanitizer.Redact(diagnosticMessage),
            innerException);

    private void LogRequestFailure(
        NeteaseEndpoint endpoint,
        NeteaseApiException exception,
        long elapsedMilliseconds,
        string correlationId,
        string? responseDiagnostic) =>
        _logger.LogError(
            "Netease request failed. Operation={Operation} Method={Method} Host={Host} Endpoint={Endpoint} HttpStatus={HttpStatus} ApiCode={ApiCode} ErrorKind={ErrorKind} Diagnostic={Diagnostic} ElapsedMs={ElapsedMs} CorrelationId={CorrelationId} ResponseJson={ResponseJson:l}",
            endpoint.Name,
            endpoint.Method.Method,
            GetBaseUri(endpoint).Host,
            endpoint.Path,
            exception.HttpStatus is null ? null : (int)exception.HttpStatus,
            exception.ApiCode,
            exception.Kind,
            DiagnosticSanitizer.Redact(exception.DiagnosticMessage),
            elapsedMilliseconds,
            correlationId,
            responseDiagnostic ?? string.Empty);

    private static string? Truncate(string? value) =>
        value is null || value.Length <= 512 ? value : value[..512];

    private static void ValidateBaseUri(NeteaseApiOptions options)
    {
        if (!IsAllowedOfficialUri(options.BaseUri, options) ||
            !IsAllowedOfficialUri(options.InterfaceBaseUri, options))
        {
            throw new InvalidOperationException("Netease API Host 必须是允许列表中的 HTTPS 网易云官方域名。");
        }
    }

    private static bool IsAllowedOfficialUri(Uri uri, NeteaseApiOptions options) =>
        uri.Scheme == Uri.UriSchemeHttps && options.AllowedCookieHosts.Contains(uri.Host);
}
