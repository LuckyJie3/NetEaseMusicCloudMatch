using NeteaseMusicCloudMatch.Core.Models;

namespace NeteaseMusicCloudMatch.App.ViewModels;

public sealed class CloudSongRowViewModel
{
    public CloudSongRowViewModel(CloudSong model)
    {
        Model = model;
    }

    public CloudSong Model { get; }

    public int Index => Model.SourceIndex + 1;

    public string Name => Display(Model.Name);

    public string Artists => Display(Model.Artists);

    public string Album => Display(Model.Album);

    public string FileName => Display(Model.FileName);

    public string SongId => Model.SongId?.ToString() ?? "—";

    public string CurrentMatch => Model.CurrentMatchedSongId?.ToString() ?? "—";

    public Uri? ArtworkUrl => Model.ArtworkUrl;

    public string FileSize => Model.FileSize is { } size ? FormatSize(size) : "—";

    public string Bitrate => Model.Bitrate is { } bitrate ? $"{bitrate / 1000d:0.#} kbps" : "—";

    public string UploadedAt => Model.UploadedAt?.ToLocalTime().ToString("yyyy-MM-dd HH:mm") ?? "—";

    public string Duration => Model.DurationMilliseconds is { } milliseconds
        ? $"{TimeSpan.FromMilliseconds(milliseconds):m\\:ss}"
        : "—";

    private static string Display(string? value) => string.IsNullOrWhiteSpace(value) ? "—" : value;

    private static string FormatSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
