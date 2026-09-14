using NeteaseMusicCloudMatch.Core.Exceptions;
using NeteaseMusicCloudMatch.Core.Models;
using NeteaseMusicCloudMatch.Core.Security;
using NeteaseMusicCloudMatch.Core.Services;

namespace NeteaseMusicCloudMatch.Infrastructure.Authentication;

public sealed class AuthService : IAuthService, IAuthSessionAccessor
{
    private readonly INeteaseApiClient _apiClient;
    private readonly ICookieStore _cookieStore;
    private readonly object _sync = new();
    private string? _cookie;
    private UserProfile? _currentUser;

    public AuthService(INeteaseApiClient apiClient, ICookieStore cookieStore)
    {
        _apiClient = apiClient;
        _cookieStore = cookieStore;
    }

    public UserProfile? CurrentUser
    {
        get
        {
            lock (_sync)
            {
                return _currentUser;
            }
        }
    }

    public bool IsLoggedIn => CurrentUser is not null;

    public event EventHandler? SessionExpired;

    public async Task<UserProfile> LoginAsync(
        string cookie,
        bool rememberLogin,
        CancellationToken cancellationToken = default)
    {
        var parsedCookie = CookieParser.Parse(cookie);
        var response = await _apiClient.GetAccountAsync(parsedCookie.HeaderValue, cancellationToken).ConfigureAwait(false);
        var profile = response.Value;
        if (profile is null)
        {
            throw new NeteaseApiException(
                NeteaseErrorKind.CookieExpired,
                "Cookie 无效或已过期，请重新登录网易云网页版后复制新的 Cookie。",
                "GetAccount",
                response.CorrelationId,
                response.HttpStatus,
                response.ApiCode,
                "Account response did not contain a valid profile");
        }

        if (rememberLogin)
        {
            await _cookieStore.SaveAsync(parsedCookie.HeaderValue, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _cookieStore.DeleteAsync(cancellationToken).ConfigureAwait(false);
        }

        lock (_sync)
        {
            _cookie = parsedCookie.HeaderValue;
            _currentUser = profile;
        }

        return profile;
    }

    public async Task<UserProfile?> TryRestoreAsync(CancellationToken cancellationToken = default)
    {
        var cookie = await _cookieStore.LoadAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return null;
        }

        try
        {
            return await LoginAsync(cookie, true, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is FormatException ||
                                          exception is NeteaseApiException { Kind: NeteaseErrorKind.CookieExpired or NeteaseErrorKind.Unauthorized })
        {
            await InvalidateAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
    }

    public Task LogoutAsync(CancellationToken cancellationToken = default) => ClearSessionAsync(false, cancellationToken);

    (string Cookie, UserProfile User) IAuthSessionAccessor.GetRequiredSession()
    {
        lock (_sync)
        {
            if (_cookie is not null && _currentUser is not null)
            {
                return (_cookie, _currentUser);
            }
        }

        throw new NeteaseApiException(
            NeteaseErrorKind.Unauthorized,
            "请先使用 Cookie 登录。",
            "Session",
            Guid.NewGuid().ToString("N"));
    }

    public Task InvalidateAsync(CancellationToken cancellationToken = default) => ClearSessionAsync(true, cancellationToken);

    private async Task ClearSessionAsync(bool notifyExpired, CancellationToken cancellationToken)
    {
        var hadSession = false;
        lock (_sync)
        {
            hadSession = _cookie is not null || _currentUser is not null;
            _cookie = null;
            _currentUser = null;
        }

        await _cookieStore.DeleteAsync(cancellationToken).ConfigureAwait(false);
        if (hadSession && notifyExpired)
        {
            SessionExpired?.Invoke(this, EventArgs.Empty);
        }
    }
}
