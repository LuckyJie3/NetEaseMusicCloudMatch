using NeteaseMusicCloudMatch.Core.Models;

namespace NeteaseMusicCloudMatch.Core.Services;

public interface INeteaseApiClient
{
    Task<ApiCallResult<UserProfile?>> GetAccountAsync(string cookie, CancellationToken cancellationToken = default);

    Task<ApiCallResult<CloudPage>> GetCloudSongsAsync(string cookie, int limit, int offset, CancellationToken cancellationToken = default);

    Task<ApiCallResult<CloudSong?>> GetCloudSongByIdAsync(string cookie, long cloudSongId, CancellationToken cancellationToken = default);

    Task<ApiCallResult<SongDetails?>> GetSongDetailAsync(string cookie, long targetSongId, CancellationToken cancellationToken = default);

    Task<ApiCallResult<MatchResult>> MatchCloudSongAsync(
        string cookie,
        long userId,
        long cloudSongId,
        long targetSongId,
        CancellationToken cancellationToken = default);
}
