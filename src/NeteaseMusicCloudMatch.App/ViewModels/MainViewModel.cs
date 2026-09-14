using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NeteaseMusicCloudMatch.App.Services;
using NeteaseMusicCloudMatch.Core.Models;
using NeteaseMusicCloudMatch.Core.Services;

namespace NeteaseMusicCloudMatch.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly IUserDialogService _dialogs;
    private readonly ILogger<MainViewModel> _logger;

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private bool _isInitializing = true;

    [ObservableProperty]
    private object? _currentPage;

    [ObservableProperty]
    private UserProfile? _currentUser;

    [ObservableProperty]
    private string _selectedNavigation = "云盘";

    public MainViewModel(
        IAuthService authService,
        IUserDialogService dialogs,
        LoginViewModel login,
        CloudDriveViewModel cloudDrive,
        LogViewModel log,
        SettingsViewModel settings,
        ILogger<MainViewModel> logger)
    {
        _authService = authService;
        _dialogs = dialogs;
        _logger = logger;
        Login = login;
        CloudDrive = cloudDrive;
        Log = log;
        Settings = settings;
        _currentPage = cloudDrive;

        Login.LoginSucceeded += OnLoginSucceeded;
        _authService.SessionExpired += OnSessionExpired;
    }

    public LoginViewModel Login { get; }

    public CloudDriveViewModel CloudDrive { get; }

    public LogViewModel Log { get; }

    public SettingsViewModel Settings { get; }

    public string DisplayName => string.IsNullOrWhiteSpace(CurrentUser?.Nickname) ? "网易云用户" : CurrentUser.Nickname;

    public string UserIdText => CurrentUser is null ? "UID —" : $"UID {CurrentUser.UserId}";

    public Uri? AvatarUrl => CurrentUser?.AvatarUrl;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsInitializing = true;
        try
        {
            var restored = await _authService.TryRestoreAsync(cancellationToken);
            if (restored is not null)
            {
                await ApplyLoggedInAsync(restored, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning("Session restore failed safely. ErrorType={ErrorType}", exception.GetType().Name);
        }
        finally
        {
            IsInitializing = false;
        }
    }

    [RelayCommand]
    private void ShowCloud()
    {
        SelectedNavigation = "云盘";
        CurrentPage = CloudDrive;
    }

    [RelayCommand]
    private async Task ShowLogAsync()
    {
        SelectedNavigation = "操作日志";
        CurrentPage = Log;
        await Log.RefreshAsync();
    }

    [RelayCommand]
    private void ShowSettings()
    {
        SelectedNavigation = "设置";
        CurrentPage = Settings;
    }

    [RelayCommand]
    private async Task LogoutAsync(CancellationToken cancellationToken)
    {
        if (!await _dialogs.ConfirmAsync(
                "确定要退出登录并删除本机保存的登录状态吗？",
                "退出登录",
                cancellationToken))
        {
            return;
        }

        await _authService.LogoutAsync(cancellationToken);
        ApplyLoggedOut();
    }

    private async void OnLoginSucceeded(object? sender, UserProfile profile)
    {
        await ApplyLoggedInAsync(profile, CancellationToken.None);
    }

    private void OnSessionExpired(object? sender, EventArgs eventArgs)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            ApplyLoggedOut();
            Login.StatusMessage = "Cookie 无效或已过期，请重新登录网易云网页版后复制新的 Cookie。";
        });
    }

    private async Task ApplyLoggedInAsync(UserProfile profile, CancellationToken cancellationToken)
    {
        CurrentUser = profile;
        IsLoggedIn = true;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(UserIdText));
        OnPropertyChanged(nameof(AvatarUrl));
        ShowCloud();
        await CloudDrive.LoadInitialAsync(cancellationToken);
    }

    private void ApplyLoggedOut()
    {
        CloudDrive.Clear();
        CurrentUser = null;
        IsLoggedIn = false;
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(UserIdText));
        OnPropertyChanged(nameof(AvatarUrl));
    }
}
