using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using NeteaseMusicCloudMatch.App;
using NeteaseMusicCloudMatch.App.Services;
using NeteaseMusicCloudMatch.App.ViewModels;
using NeteaseMusicCloudMatch.App.Views;
using NeteaseMusicCloudMatch.Core.Models;

namespace NeteaseMusicCloudMatch.UiPreview;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine("Usage: UiPreview <output-directory>");
            return 1;
        }

        var outputDirectory = Path.GetFullPath(args[0]);
        Directory.CreateDirectory(outputDirectory);

        var application = new NeteaseMusicCloudMatch.App.App();
        application.InitializeComponent();

        var themeService = new ThemeService();
        themeService.Apply(NeteaseMusicCloudMatch.App.Services.ThemeMode.Light, false);

        var login = new LoginView
        {
            DataContext = new LoginPreviewViewModel()
        };
        Render(login, 1220, 752, Path.Combine(outputDirectory, "login-light.png"));

        var cloudViewModel = new CloudPreviewViewModel();
        var cloud = new CloudDriveView
        {
            DataContext = cloudViewModel
        };
        Render(cloud, 1060, 556, Path.Combine(outputDirectory, "cloud-min-light.png"));
        Render(new CloudDriveView { DataContext = cloudViewModel }, 1220, 676, Path.Combine(outputDirectory, "cloud-wide-light.png"));

        var logs = new LogView
        {
            DataContext = new LogPreviewViewModel()
        };
        Render(logs, 1220, 676, Path.Combine(outputDirectory, "logs-light.png"), expandFirstLog: true);

        Render(
            new SettingsView { DataContext = new SettingsPreviewViewModel(NeteaseMusicCloudMatch.App.Services.ThemeMode.Light) },
            1220,
            900,
            Path.Combine(outputDirectory, "settings-about-light.png"));
        Render(
            CreateNavigationPreview(false),
            760,
            90,
            Path.Combine(outputDirectory, "navigation-light.png"));

        themeService.Apply(NeteaseMusicCloudMatch.App.Services.ThemeMode.Dark, false);
        Render(new LoginView { DataContext = new LoginPreviewViewModel() }, 1220, 752, Path.Combine(outputDirectory, "login-dark.png"));
        Render(new CloudDriveView { DataContext = new CloudPreviewViewModel() }, 1060, 556, Path.Combine(outputDirectory, "cloud-min-dark.png"));
        Render(
            new LogView { DataContext = new LogPreviewViewModel() },
            1220,
            676,
            Path.Combine(outputDirectory, "logs-dark.png"),
            expandFirstLog: true);
        Render(
            new SettingsView { DataContext = new SettingsPreviewViewModel(NeteaseMusicCloudMatch.App.Services.ThemeMode.Dark) },
            1220,
            900,
            Path.Combine(outputDirectory, "settings-about-dark.png"));
        Render(
            CreateNavigationPreview(true),
            760,
            90,
            Path.Combine(outputDirectory, "navigation-dark.png"));
        return 0;
    }

    private static FrameworkElement CreateNavigationPreview(bool dark)
    {
        var navigation = new StackPanel { Orientation = Orientation.Horizontal };
        foreach (var label in new[] { "云盘", "日志", "设置" })
        {
            var button = new Wpf.Ui.Controls.Button
            {
                Content = label,
                Style = (Style)Application.Current.Resources["TopNavigationButton"]
            };
            if (label == "云盘")
            {
                button.Background = (Brush)Application.Current.Resources["SurfaceBrush"];
                button.Foreground = (Brush)Application.Current.Resources["AccentBrush"];
                button.FontWeight = FontWeights.SemiBold;
            }

            navigation.Children.Add(button);
        }

        return new Border
        {
            Background = (Brush)Application.Current.Resources["NavigationSurfaceBrush"],
            CornerRadius = new CornerRadius(13),
            Padding = new Thickness(3),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = navigation
        };
    }

    private static void Render(
        FrameworkElement content,
        int width,
        int height,
        string outputPath,
        bool expandFirstLog = false)
    {
        var root = new Border
        {
            Width = width,
            Height = height,
            Background = (Brush)Application.Current.Resources["WindowBackgroundBrush"],
            Child = content
        };
        root.Measure(new Size(width, height));
        root.Arrange(new Rect(0, 0, width, height));
        root.UpdateLayout();

        // Let bindings and item templates finish materializing before inspecting
        // the visual tree. The preview renderer has no normal WPF message loop.
        Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
        root.UpdateLayout();

        if (expandFirstLog && FindVisualChild<Expander>(root) is { } expander)
        {
            expander.IsExpanded = true;
            expander.InvalidateMeasure();
            content.InvalidateMeasure();
            root.InvalidateMeasure();
            root.Measure(new Size(width, height));
            root.Arrange(new Rect(0, 0, width, height));
            root.UpdateLayout();
            Dispatcher.CurrentDispatcher.Invoke(static () => { }, DispatcherPriority.ApplicationIdle);
            root.UpdateLayout();
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
    }

    private static T? FindVisualChild<T>(DependencyObject parent)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
        {
            var child = VisualTreeHelper.GetChild(parent, index);
            if (child is T match)
            {
                return match;
            }

            if (FindVisualChild<T>(child) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    private sealed class LoginPreviewViewModel
    {
        public string CookieText { get; set; } = string.Empty;

        public bool RememberLogin { get; set; } = true;

        public string StatusMessage { get; set; } = "粘贴 Cookie 后即可登录。";

        public ICommand? OpenNeteaseWebsiteCommand => null;

        public ICommand? ClearCookieCommand => null;

        public ICommand? LoginCommand => null;
    }

    private sealed class CloudPreviewViewModel
    {
        public CloudPreviewViewModel()
        {
            Songs =
            [
                Row(0, 30780434, 30780434, "晴天", "周杰伦", "叶惠美", "周杰伦 - 晴天.flac", 36_814_224, 269_000),
                Row(1, 186137, 186137, "双截棍", "周杰伦", "范特西", "双截棍.mp3", 8_632_104, 210_000),
                Row(2, 277709, 277709, "江南", "林俊杰", "第二天堂", "林俊杰 - 江南.m4a", 10_936_320, 267_000)
            ];
            SongsView = CollectionViewSource.GetDefaultView(Songs);
            SelectedSong = Songs[0];
            Match = new MatchPreviewViewModel(SelectedSong);
        }

        public ObservableCollection<CloudSongRowViewModel> Songs { get; }

        public ICollectionView SongsView { get; }

        public CloudSongRowViewModel SelectedSong { get; set; }

        public MatchPreviewViewModel Match { get; }

        public string SearchText { get; set; } = string.Empty;

        public string StatusMessage => "已加载第 1 / 3 页，共 426 首。";

        public int TotalSongCount => 426;

        public string SongCountText => "共 426 首";

        public int CurrentPage => 1;

        public ICommand? PreviousPageCommand => null;

        public ICommand? NextPageCommand => null;

        private static CloudSongRowViewModel Row(
            int index,
            long cloudId,
            long matchedId,
            string name,
            string artist,
            string album,
            string fileName,
            long fileSize,
            long duration) =>
            new(new CloudSong(
                index,
                cloudId,
                matchedId,
                name,
                artist,
                album,
                fileName,
                fileSize,
                320_000,
                DateTimeOffset.Now.AddDays(-index - 1),
                null,
                duration));
    }

    private sealed class MatchPreviewViewModel(CloudSongRowViewModel selectedSong)
    {
        public CloudSongRowViewModel SelectedCloudSong { get; } = selectedSong;

        public string TargetInput { get; set; } = "https://music.163.com/#/song?id=185809";

        public SongDetails TargetPreview { get; } = new(185809, "七里香", "周杰伦", "七里香", null, 299_000);

        public string StatusMessage => "请核对歌曲信息，确认无误后再匹配。";

        public ICommand? PreviewTargetCommand => null;

        public ICommand? ConfirmMatchCommand => null;

        public ICommand? CancelMatchCommand => null;
    }

    private sealed class LogPreviewViewModel
    {
        public string StatusMessage => "共 4 条，最新操作显示在最前。";

        public ObservableCollection<ApplicationLogEntry> Entries { get; } =
        [
            new(
                DateTimeOffset.Now,
                ApplicationLogSeverity.Error,
                "匹配歌曲",
                "当前歌曲不支持匹配",
                "GET /api/cloud/user/song/match  ·  HTTP 200  ·  API 400  ·  追踪号 531fb78c0af842d09fb637e052718a45",
                """
                {
                  "code": 400,
                  "message": "当前歌曲不支持匹配",
                  "MUSIC_U": "[REDACTED]"
                }
                """,
                true),
            new(DateTimeOffset.Now.AddSeconds(-8), ApplicationLogSeverity.Information, "查询目标歌曲", "请求成功 · 45 ms", "GET /api/song/detail/  ·  HTTP 200  ·  API 200"),
            new(DateTimeOffset.Now.AddMinutes(-2), ApplicationLogSeverity.Warning, "Cookie 登录", "粘贴的 Cookie 格式不正确", "错误类型：FormatException"),
            new(DateTimeOffset.Now.AddMinutes(-3), ApplicationLogSeverity.Information, "加载云盘", "请求成功 · 1347 ms", "POST /api/v1/cloud/get  ·  HTTP 200  ·  API 200")
        ];

        public ICommand? RefreshCommand => null;

        public ICommand? ClearLogsCommand => null;

        public ICommand? OpenLogDirectoryCommand => null;
    }

    private sealed class SettingsPreviewViewModel(NeteaseMusicCloudMatch.App.Services.ThemeMode theme)
    {
        public bool IsSystemTheme => theme == NeteaseMusicCloudMatch.App.Services.ThemeMode.System;

        public bool IsLightTheme => theme == NeteaseMusicCloudMatch.App.Services.ThemeMode.Light;

        public bool IsDarkTheme => theme == NeteaseMusicCloudMatch.App.Services.ThemeMode.Dark;

        public string ReadApiHost => "https://music.163.com";

        public string MatchApiHost => "https://interface.music.163.com";

        public string LogDirectory => @"C:\Users\User\AppData\Local\NeteaseMusicCloudMatch\Logs";

        public string Version => "1.0.0";

        public ICommand? UseSystemThemeCommand => null;

        public ICommand? UseLightThemeCommand => null;

        public ICommand? UseDarkThemeCommand => null;

        public ICommand? OpenLogDirectoryCommand => null;

        public ICommand? OpenOriginalProjectCommand => null;

        public ICommand? OpenMacReferenceCommand => null;

        public ICommand? OpenWpfUiCommand => null;
    }
}
