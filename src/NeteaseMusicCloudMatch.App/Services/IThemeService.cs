namespace NeteaseMusicCloudMatch.App.Services;

using System.Windows;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public interface IThemeService
{
    ThemeMode Current { get; }

    ThemeMode Load();

    void Apply(ThemeMode mode, bool persist = true);

    void AttachWindow(Window window);
}
