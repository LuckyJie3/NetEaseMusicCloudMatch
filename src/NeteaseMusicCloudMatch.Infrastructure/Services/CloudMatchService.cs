using NeteaseMusicCloudMatch.Core.Exceptions;
using NeteaseMusicCloudMatch.Core.Models;
using NeteaseMusicCloudMatch.Core.Services;
using NeteaseMusicCloudMatch.Infrastructure.Authentication;

namespace NeteaseMusicCloudMatch.Infrastructure.Services;

public sealed class CloudMatchService : ICloudMatchService, IDisposable
{
    private readonly INeteaseApiClient _apiClient;
    private readonly IAuthSessionAccessor _session;
    private readonly SemaphoreSlim _singleWrite = new(1, 1);

    public CloudMatchService(INeteaseApiClient apiClient, IAuthSessionAccessor session)
    {
        _apiClient = apiClient;
        _session = session;
    }

    public Task<MatchResult> MatchAsync(
        long userId,
        long cloudSongId,
        long targetSongId,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSongId);
        return SendMatchAsync(userId, cloudSongId, targetSongId, cancellationToken);
    }

    public Task<MatchResult> CancelAsync(
        long userId,
        long cloudSongId,
        CancellationToken cancellationToken = default) =>
        SendMatchAsync(userId, cloudSongId, 0, cancellationToken);

    private async Task<MatchResult> SendMatchAsync(
        long userId,
        long cloudSongId,
        long targetSongId,
        CancellationToken cancellationToken)
    {
        var (cookie, currentUser) = _session.GetRequiredSession();
        if (userId != currentUser.UserId)
        {
            throw new NeteaseApiException(
                NeteaseErrorKind.Unauthorized,
                "当前账号信息已变化，请重新登录。",
                "MatchCloudSong",
                Guid.NewGuid().ToString("N"));
        }

        await _singleWrite.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var result = await _apiClient.MatchCloudSongAsync(
                cookie,
                userId,
                cloudSongId,
                targetSongId,
                cancellationToken).ConfigureAwait(false);
            return result.Value;
        }
        catch (NeteaseApiException exception) when (exception.Kind is NeteaseErrorKind.CookieExpired or NeteaseErrorKind.Unauthorized)
        {
            await _session.InvalidateAsync(CancellationToken.None).ConfigureAwait(false);
            throw;
        }
        finally
        {
            _singleWrite.Release();
        }
    }

    public void Dispose() => _singleWrite.Dispose();
}
