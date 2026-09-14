using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NeteaseMusicCloudMatch.App.Services;
using NeteaseMusicCloudMatch.Core.Models;
using NeteaseMusicCloudMatch.Core.Services;

namespace NeteaseMusicCloudMatch.App.ViewModels;

public sealed partial class LoginViewModel : ObservableObject
{
    private static readonly Uri NeteaseWebsite = new("https://music.163.com/");
    private readonly IAuthService _authService;
    private readonly IExternalNavigationService _navigation;
    private readonly ILogger<LoginViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _cookieText = string.Empty;

    [ObservableProperty]
    private bool _rememberLogin;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "请从网易云音乐网页版复制完整 Cookie。";

    public LoginViewModel(
        IAuthService authService,
        IExternalNavigationService navigation,
        ILogger<LoginViewModel> logger)
    {
        _authService = authService;
        _navigation = navigation;
        _logger = logger;
    }

    public event EventHandler<UserProfile>? LoginSucceeded;

    [RelayCommand]
    private void OpenNeteaseWebsite() => _navigation.OpenUrl(NeteaseWebsite);

    [RelayCommand]
    private void ClearCookie()
    {
        CookieText = string.Empty;
        StatusMessage = "Cookie 已从输入框清空。";
    }

    private bool CanLogin() => !IsBusy && !string.IsNullOrWhiteSpace(CookieText);

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync(CancellationToken cancellationToken)
    {
        IsBusy = true;
        StatusMessage = "正在验证 Cookie…";
        try
        {
            var profile = await _authService.LoginAsync(CookieText, RememberLogin, cancellationToken);
            CookieText = string.Empty;
            StatusMessage = "登录成功。";
            LoginSucceeded?.Invoke(this, profile);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusMessage = ErrorMessageFormatter.Format(exception);
            _logger.LogWarning(
                "Cookie login failed safely. ErrorType={ErrorType}",
                exception.GetType().Name);
        }
        finally
        {
            IsBusy = false;
        }
    }
}
