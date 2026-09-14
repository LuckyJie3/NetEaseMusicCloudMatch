using NeteaseMusicCloudMatch.Core.Models;

namespace NeteaseMusicCloudMatch.Infrastructure.Authentication;

public interface IAuthSessionAccessor
{
    (string Cookie, UserProfile User) GetRequiredSession();

    Task InvalidateAsync(CancellationToken cancellationToken = default);
}
