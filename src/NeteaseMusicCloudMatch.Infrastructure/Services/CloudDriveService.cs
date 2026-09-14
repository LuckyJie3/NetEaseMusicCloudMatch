using NeteaseMusicCloudMatch.Core.Exceptions;
using NeteaseMusicCloudMatch.Core.Models;
using NeteaseMusicCloudMatch.Core.Services;
using NeteaseMusicCloudMatch.Infrastructure.Authentication;

namespace NeteaseMusicCloudMatch.Infrastructure.Services;

public sealed class CloudDriveService : ICloudDriveService
{
    private readonly INeteaseApiClient _apiClient;
    private readonly IAuthSessionAccessor _session;

    public CloudDriveService(INeteaseApiClient apiClient, IAuthSessionAccessor session)
    {
        _apiClient = apiClient;
        _session = session;
    }

    public async Task<CloudPage> GetPageAsync(int page, int limit = 200, CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);
        var (cookie, _) = _session.GetRequiredSession();
        try
        {
            var result = await _apiClient.GetCloudSongsAsync(cookie, limit, (page - 1) * limit, cancellationToken).ConfigureAwait(false);
            return result.Value;
        }
        catch (NeteaseApiException exception) when (exception.Kind is NeteaseErrorKind.CookieExpired or NeteaseErrorKind.Unauthorized)
        {
            await _session.InvalidateAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }

    public async Task<CloudSong> GetByIdAsync(long cloudSongId, CancellationToken cancellationToken = default)
    {
        var (cookie, _) = _session.GetRequiredSession();
        try
        {
            var result = await _apiClient.GetCloudSongByIdAsync(cookie, cloudSongId, cancellationToken).ConfigureAwait(false);
            return result.Value ?? throw new NeteaseApiException(
                NeteaseErrorKind.CloudSongNotFound,
                "这首云盘歌曲不存在或已经发生变化，请刷新列表。",
                "GetCloudSongById",
                result.CorrelationId,
                result.HttpStatus,
                result.ApiCode);
        }
        catch (NeteaseApiException exception) when (exception.Kind is NeteaseErrorKind.CookieExpired or NeteaseErrorKind.Unauthorized)
        {
            await _session.InvalidateAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
    }
}
