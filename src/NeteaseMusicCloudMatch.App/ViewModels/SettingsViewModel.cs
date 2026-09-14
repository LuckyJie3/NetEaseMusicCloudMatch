using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeteaseMusicCloudMatch.App.Services;

namespace NeteaseMusicCloudMatch.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly IThemeService _themeService;
    private readonly IExternalNavigationService _externalNavigation;

    [ObservableProperty]
    private ThemeChoice? _selectedTheme;

    public SettingsViewModel(IThemeService themeService, IExternalNavigationService externalNavigation)
    {
        _themeService = themeService;
        _externalNavigation = externalNavigation;
        Themes =
        [
            new ThemeChoice(ThemeMode.System, "跟随系统"),
            new ThemeChoice(ThemeMode.Light, "浅色"),
            new ThemeChoice(ThemeMode.Dark, "深色")
        ];
        var loaded = _themeService.Load();
        _selectedTheme = Themes.First(choice => choice.Mode == loaded);
    }

    public IReadOnlyList<ThemeChoice> Themes { get; }

    public string ReadApiHost => "https://music.163.com";

    public string MatchApiHost => "https://interface.music.163.com";

    public string LogDirectory => AppPaths.LogDirectory;

    public string Version => typeof(SettingsViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.0.0";

    public bool IsSystemTheme => SelectedTheme?.Mode == ThemeMode.System;

    public bool IsLightTheme => SelectedTheme?.Mode == ThemeMode.Light;

    public bool IsDarkTheme => SelectedTheme?.Mode == ThemeMode.Dark;

    partial void OnSelectedThemeChanged(ThemeChoice? value)
    {
        OnPropertyChanged(nameof(IsSystemTheme));
        OnPropertyChanged(nameof(IsLightTheme));
        OnPropertyChanged(nameof(IsDarkTheme));

        if (value is not null)
        {
            _themeService.Apply(value.Mode);
        }
    }

    [RelayCommand]
    private void UseSystemTheme() => SelectTheme(ThemeMode.System);

    [RelayCommand]
    private void UseLightTheme() => SelectTheme(ThemeMode.Light);

    [RelayCommand]
    private void UseDarkTheme() => SelectTheme(ThemeMode.Dark);

    [RelayCommand]
    private void OpenLogDirectory() => _externalNavigation.OpenDirectory(LogDirectory);

    [RelayCommand]
    private void OpenOriginalProject() =>
        _externalNavigation.OpenUrl(new Uri("https://github.com/wuhenge/NeteaseMusicCloudMatch"));

    [RelayCommand]
    private void OpenMacReference() =>
        _externalNavigation.OpenUrl(new Uri("https://github.com/joeyee233/NetEaseMusicCloudMatch"));

    [RelayCommand]
    private void OpenWpfUi() =>
        _externalNavigation.OpenUrl(new Uri("https://github.com/lepoco/wpfui"));

    private void SelectTheme(ThemeMode mode) =>
        SelectedTheme = Themes.First(choice => choice.Mode == mode);
}

public sealed record ThemeChoice(ThemeMode Mode, string DisplayName);
