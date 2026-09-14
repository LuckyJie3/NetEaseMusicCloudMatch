using System.Globalization;

namespace NeteaseMusicCloudMatch.Core.Parsing;

public sealed record SongIdParseResult(bool Success, long? SongId, string? Error)
{
    public static SongIdParseResult Valid(long songId) => new(true, songId, null);

    public static SongIdParseResult Invalid(string error) => new(false, null, error);
}

public static class SongIdParser
{
    private static readonly string[] RejectedKinds = ["playlist", "album", "toplist"];

    public static SongIdParseResult Parse(string? input)
    {
        var value = input?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return SongIdParseResult.Invalid("请输入歌曲 ID 或网易云单曲链接。");
        }

        if (value.All(char.IsAsciiDigit))
        {
            return TryPositiveInt64(value, out var numericId)
                ? SongIdParseResult.Valid(numericId)
                : SongIdParseResult.Invalid("歌曲 ID 必须是大于 0 的整数。");
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) ||
            !uri.IsDefaultPort ||
            !IsNeteaseMusicHost(uri.Host))
        {
            return SongIdParseResult.Invalid("这不是有效的网易云音乐单曲链接。");
        }

        var route = GetRoute(uri);
        var normalizedPath = route.Path.TrimEnd('/').ToLowerInvariant();

        if (RejectedKinds.Any(kind => HasPathSegment(normalizedPath, kind)))
        {
            return SongIdParseResult.Invalid("请输入单曲链接，不能使用歌单、专辑或排行榜链接。");
        }

        if (!HasPathSegment(normalizedPath, "song"))
        {
            return SongIdParseResult.Invalid("链接不是网易云音乐单曲页面。");
        }

        var idValue = GetQueryValue(route.Query, "id");
        if (idValue is null)
        {
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var songIndex = Array.FindLastIndex(segments, segment => segment.Equals("song", StringComparison.OrdinalIgnoreCase));
            if (songIndex >= 0 && songIndex + 1 < segments.Length)
            {
                idValue = segments[songIndex + 1];
            }
        }

        return TryPositiveInt64(idValue, out var songId)
            ? SongIdParseResult.Valid(songId)
            : SongIdParseResult.Invalid("单曲链接中没有有效的歌曲 ID。");
    }

    private static bool IsNeteaseMusicHost(string host) =>
        host.Equals("music.163.com", StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith(".music.163.com", StringComparison.OrdinalIgnoreCase);

    private static bool HasPathSegment(string path, string expected) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries)
            .Any(segment => segment.Equals(expected, StringComparison.OrdinalIgnoreCase));

    private static (string Path, string Query) GetRoute(Uri uri)
    {
        var fragment = uri.Fragment.TrimStart('#');
        if (fragment.StartsWith('/'))
        {
            var separator = fragment.IndexOf('?');
            return separator >= 0
                ? (fragment[..separator], fragment[separator..])
                : (fragment, string.Empty);
        }

        return (uri.AbsolutePath, uri.Query);
    }

    private static string? GetQueryValue(string query, string expectedName)
    {
        foreach (var pair in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var rawName = separator >= 0 ? pair[..separator] : pair;
            if (!Uri.UnescapeDataString(rawName).Equals(expectedName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return separator >= 0 ? Uri.UnescapeDataString(pair[(separator + 1)..]) : string.Empty;
        }

        return null;
    }

    private static bool TryPositiveInt64(string? value, out long songId) =>
        long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out songId) && songId > 0;
}
