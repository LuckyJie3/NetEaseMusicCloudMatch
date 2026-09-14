using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

namespace NeteaseMusicCloudMatch.App.Services;

public sealed class ThemeService : IThemeService
{
    private sealed record ThemeSettings(string Theme);

    private Window? _window;
    private bool _isWatchingSystemTheme;

    public ThemeService()
    {
        ApplicationThemeManager.Changed += (theme, _) =>
        {
            UpdateSemanticBrushes(theme == ApplicationTheme.Dark);
            ApplyBrandAccent(theme);
        };
    }

    public ThemeMode Current { get; private set; } = ThemeMode.System;

    public ThemeMode Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsPath))
            {
                var json = File.ReadAllText(AppPaths.SettingsPath);
                var settings = JsonSerializer.Deserialize<ThemeSettings>(json);
                if (Enum.TryParse<ThemeMode>(settings?.Theme, true, out var mode))
                {
                    Apply(mode, false);
                    return mode;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // 设置文件损坏时回退到系统主题；这里不包含敏感数据。
        }

        Apply(ThemeMode.System, false);
        return ThemeMode.System;
    }

    public void Apply(ThemeMode mode, bool persist = true)
    {
        Current = mode;
        var dark = mode == ThemeMode.Dark || (mode == ThemeMode.System && IsSystemDark());

        if (_isWatchingSystemTheme && _window?.IsLoaded == true)
        {
            SystemThemeWatcher.UnWatch(_window);
            _isWatchingSystemTheme = false;
        }

        if (mode == ThemeMode.System)
        {
            ApplicationThemeManager.Apply(
                dark ? ApplicationTheme.Dark : ApplicationTheme.Light,
                WindowBackdropType.Mica,
                false);
            if (_window?.IsLoaded == true)
            {
                SystemThemeWatcher.Watch(_window, WindowBackdropType.Mica, false);
                _isWatchingSystemTheme = true;
            }
        }
        else
        {
            ApplicationThemeManager.Apply(
                dark ? ApplicationTheme.Dark : ApplicationTheme.Light,
                WindowBackdropType.Mica,
                false);
        }

        UpdateSemanticBrushes(dark);
        ApplyBrandAccent(dark ? ApplicationTheme.Dark : ApplicationTheme.Light);

        if (persist)
        {
            Directory.CreateDirectory(AppPaths.DataDirectory);
            File.WriteAllText(
                AppPaths.SettingsPath,
                JsonSerializer.Serialize(new ThemeSettings(mode.ToString())));
        }
    }

    public void AttachWindow(Window window)
    {
        _window = window;
        if (window.IsLoaded)
        {
            Apply(Current, false);
        }
    }

    private static void UpdateSemanticBrushes(bool dark)
    {
        var resources = Application.Current.Resources;
        resources["WindowBackgroundBrush"] = Brush(dark ? "#17171A" : "#F5F5F7");
        resources["SurfaceBrush"] = Brush(dark ? "#222225" : "#FFFFFF");
        resources["SurfaceAltBrush"] = Brush(dark ? "#2C2C30" : "#F1F1F3");
        resources["ElevatedSurfaceBrush"] = Brush(dark ? "#27272B" : "#FCFCFD");
        resources["NavigationSurfaceBrush"] = Brush(dark ? "#D92C2C30" : "#EAF1F1F3");
        resources["PrimaryTextBrush"] = Brush(dark ? "#F6F6F7" : "#19191C");
        resources["SecondaryTextBrush"] = Brush(dark ? "#A7A7AE" : "#73737A");
        resources["BorderBrush"] = Brush(dark ? "#3A3A40" : "#E8E8EB");
        resources["AccentBrush"] = Brush("#EC4141");
        resources["AccentHoverBrush"] = Brush("#D93636");
        resources["AccentSoftBrush"] = Brush(dark ? "#2CEC4141" : "#12EC4141");
        resources["SelectionBrush"] = Brush(dark ? "#34EC4141" : "#14EC4141");
        resources["SuccessBrush"] = Brush(dark ? "#65D391" : "#147D46");
        resources["SuccessBackgroundBrush"] = Brush(dark ? "#173326" : "#E9F7EF");
        resources["WarningBrush"] = Brush(dark ? "#F1C75B" : "#9A6700");
        resources["WarningBackgroundBrush"] = Brush(dark ? "#3A3017" : "#FFF6D8");
        resources["DangerBrush"] = Brush(dark ? "#FF7B83" : "#C82D3A");
        resources["DangerBackgroundBrush"] = Brush(dark ? "#3D1E24" : "#FDECEF");
    }

    private static void ApplyBrandAccent(ApplicationTheme theme) =>
        ApplicationAccentColorManager.Apply(
            Color.FromRgb(236, 65, 65),
            theme,
            false,
            false);

    private static bool IsSystemDark()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
    }

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
