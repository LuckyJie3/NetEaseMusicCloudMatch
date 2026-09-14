using NeteaseMusicCloudMatch.Core.Models;

namespace NeteaseMusicCloudMatch.Core.Services;

public interface ISongService
{
    Task<SongDetails> GetDetailsAsync(long targetSongId, CancellationToken cancellationToken = default);
}
