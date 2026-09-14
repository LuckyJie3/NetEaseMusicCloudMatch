namespace NeteaseMusicCloudMatch.Core.Services;

public interface ICookieStore
{
    Task SaveAsync(string cookie, CancellationToken cancellationToken = default);

    Task<string?> LoadAsync(CancellationToken cancellationToken = default);

    Task DeleteAsync(CancellationToken cancellationToken = default);
}
