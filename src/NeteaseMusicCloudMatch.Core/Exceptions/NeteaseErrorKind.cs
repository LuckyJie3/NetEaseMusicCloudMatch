namespace NeteaseMusicCloudMatch.Core.Exceptions;

public enum NeteaseErrorKind
{
    Unauthorized,
    CookieExpired,
    SongNotFound,
    CloudSongNotFound,
    CloudSongNotMatchable,
    RateLimited,
    NetworkError,
    Timeout,
    ServerError,
    UnknownApiError
}
