using NeteaseMusicCloudMatch.Core.Models;

namespace NeteaseMusicCloudMatch.Core.Services;

public interface ICloudMatchService
{
    Task<MatchResult> MatchAsync(long userId, long cloudSongId, long targetSongId, CancellationToken cancellationToken = default);

    Task<MatchResult> CancelAsync(long userId, long cloudSongId, CancellationToken cancellationToken = default);
}
