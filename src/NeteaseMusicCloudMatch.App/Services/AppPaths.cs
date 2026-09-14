using System.IO;

namespace NeteaseMusicCloudMatch.App.Services;

public static class AppPaths
{
    public static string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NeteaseMusicCloudMatch");

    public static string LogDirectory { get; } = Path.Combine(DataDirectory, "Logs");

    public static string SettingsPath { get; } = Path.Combine(DataDirectory, "settings.json");
}
