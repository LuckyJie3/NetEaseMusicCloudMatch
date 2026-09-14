using NeteaseMusicCloudMatch.Core.Models;

namespace NeteaseMusicCloudMatch.Core.Services;

public interface ICloudDriveService
{
    Task<CloudPage> GetPageAsync(int page, int limit = 200, CancellationToken cancellationToken = default);

    Task<CloudSong> GetByIdAsync(long cloudSongId, CancellationToken cancellationToken = default);
}
