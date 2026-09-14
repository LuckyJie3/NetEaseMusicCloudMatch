using NeteaseMusicCloudMatch.Core.Exceptions;
using NeteaseMusicCloudMatch.Core.Models;
using NeteaseMusicCloudMatch.Core.Services;
using NeteaseMusicCloudMatch.Infrastructure.Authentication;

namespace NeteaseMusicCloudMatch.Infrastructure.Services;

public sealed class SongService : ISongService
{
    private readonly INeteaseApiClient _apiClient;
    private readonly IAuthSessionAccessor _session;

    public SongService(INeteaseApiClient apiClient, IAuthSessionAccessor session)
    {
        _apiClient = apiClient;
        _session = session;
    }

    public async Task<SongDetails> GetDetailsAsync(long targetSongId, CancellationToken cancellationToken = default)
    {
        var (cookie, _) = _session.GetRequiredSession();
        try
        {
            var result = await _apiClient.GetSongDetailAsync(cookie, targetSongId, cancellationToken).ConfigureAwait(false);
            return result.Value ?? throw new NeteaseApiException(
                NeteaseErrorKind.SongNotFound,
                "找不到这个网易云歌曲 ID，请检查后重试。",
                "GetSongDetail",
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
