using NeteaseMusicCloudMatch.Core.Models;

namespace NeteaseMusicCloudMatch.Core.Services;

public interface IAuthService
{
    UserProfile? CurrentUser { get; }

    bool IsLoggedIn { get; }

    event EventHandler? SessionExpired;

    Task<UserProfile> LoginAsync(string cookie, bool rememberLogin, CancellationToken cancellationToken = default);

    Task<UserProfile?> TryRestoreAsync(CancellationToken cancellationToken = default);

    Task LogoutAsync(CancellationToken cancellationToken = default);
}
