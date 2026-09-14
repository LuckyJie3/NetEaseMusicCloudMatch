using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NeteaseMusicCloudMatch.Core.Models;
using NeteaseMusicCloudMatch.Core.Services;

namespace NeteaseMusicCloudMatch.App.ViewModels;

public sealed partial class CloudDriveViewModel : ObservableObject
{
    private const int PageSize = 200;
    private readonly ICloudDriveService _cloudDriveService;
    private readonly ILogger<CloudDriveViewModel> _logger;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private CloudSongRowViewModel? _selectedSong;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand))]
    private int _currentPage = 1;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(NextPageCommand))]
    private int _totalPages = 1;

    [ObservableProperty]
    private int? _totalSongCount;

    [ObservableProperty]
    private long? _usedSize;

    [ObservableProperty]
    private long? _maxSize;

    [ObservableProperty]
    private string _statusMessage = "登录后加载云盘歌曲。";

    public CloudDriveViewModel(
        ICloudDriveService cloudDriveService,
        MatchViewModel match,
        ILogger<CloudDriveViewModel> logger)
    {
        _cloudDriveService = cloudDriveService;
        Match = match;
        _logger = logger;
        SongsView = CollectionViewSource.GetDefaultView(Songs);
        SongsView.Filter = FilterSong;
        Match.MatchCompleted += OnMatchCompleted;
    }

    public ObservableCollection<CloudSongRowViewModel> Songs { get; } = [];

    public ICollectionView SongsView { get; }

    public MatchViewModel Match { get; }

    public string CapacityText => $"{FormatSize(UsedSize)} / {FormatSize(MaxSize)}";

    public string SongCountText => TotalSongCount is { } count ? $"共 {count:N0} 首" : "数量未知";

    partial void OnSearchTextChanged(string value) => SongsView.Refresh();

    partial void OnSelectedSongChanged(CloudSongRowViewModel? value) => Match.SelectedCloudSong = value;

    partial void OnUsedSizeChanged(long? value) => OnPropertyChanged(nameof(CapacityText));

    partial void OnMaxSizeChanged(long? value) => OnPropertyChanged(nameof(CapacityText));

    partial void OnTotalSongCountChanged(int? value) => OnPropertyChanged(nameof(SongCountText));

    private bool CanRefresh() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanRefresh), IncludeCancelCommand = true)]
    private Task RefreshAsync(CancellationToken cancellationToken) => LoadPageAsync(CurrentPage, cancellationToken);

    private bool CanPreviousPage() => !IsBusy && CurrentPage > 1;

    [RelayCommand(CanExecute = nameof(CanPreviousPage))]
    private Task PreviousPageAsync(CancellationToken cancellationToken) => LoadPageAsync(CurrentPage - 1, cancellationToken);

    private bool CanNextPage() => !IsBusy && CurrentPage < TotalPages;

    [RelayCommand(CanExecute = nameof(CanNextPage))]
    private Task NextPageAsync(CancellationToken cancellationToken) => LoadPageAsync(CurrentPage + 1, cancellationToken);

    public async Task LoadInitialAsync(CancellationToken cancellationToken = default)
    {
        CurrentPage = 1;
        await LoadPageAsync(1, cancellationToken);
    }

    public void Clear()
    {
        Songs.Clear();
        SelectedSong = null;
        CurrentPage = 1;
        TotalPages = 1;
        TotalSongCount = null;
        UsedSize = null;
        MaxSize = null;
        StatusMessage = "登录后加载云盘歌曲。";
    }

    private async Task LoadPageAsync(int page, CancellationToken cancellationToken)
    {
        if (IsBusy || page < 1)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在加载云盘歌曲…";
        try
        {
            var cloudPage = await _cloudDriveService.GetPageAsync(page, PageSize, cancellationToken);
            Songs.Clear();
            foreach (var song in cloudPage.Songs)
            {
                Songs.Add(new CloudSongRowViewModel(song));
            }

            CurrentPage = page;
            TotalSongCount = cloudPage.TotalCount;
            UsedSize = cloudPage.UsedSize;
            MaxSize = cloudPage.MaxSize;
            TotalPages = Math.Max(1, (int)Math.Ceiling((cloudPage.TotalCount ?? cloudPage.Songs.Count) / (double)PageSize));
            StatusMessage = $"已加载第 {CurrentPage} / {TotalPages} 页，共 {TotalSongCount?.ToString() ?? "—"} 首。";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusMessage = ErrorMessageFormatter.Format(exception);
            _logger.LogWarning("Cloud page load failed safely. ErrorType={ErrorType}", exception.GetType().Name);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool FilterSong(object item)
    {
        if (item is not CloudSongRowViewModel song || string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return Contains(song.Name) || Contains(song.Artists) || Contains(song.Album) || Contains(song.FileName) || Contains(song.SongId);
    }

    private bool Contains(string value) => value.Contains(SearchText.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private async void OnMatchCompleted(object? sender, MatchCompletedEventArgs eventArgs)
    {
        if (eventArgs.UpdatedSong is not null)
        {
            var index = Songs.ToList().FindIndex(song => song.Model.SongId == eventArgs.CloudSongId);
            if (index >= 0)
            {
                var original = Songs[index].Model;
                var updated = Merge(original, eventArgs.UpdatedSong);
                var row = new CloudSongRowViewModel(updated);
                Songs[index] = row;
                SelectedSong = row;
                return;
            }
        }

        await LoadPageAsync(CurrentPage, CancellationToken.None);
    }

    private static CloudSong Merge(CloudSong original, CloudSong updated) => new(
        original.SourceIndex,
        updated.SongId ?? original.SongId,
        updated.CurrentMatchedSongId ?? original.CurrentMatchedSongId,
        updated.Name ?? original.Name,
        updated.Artists ?? original.Artists,
        updated.Album ?? original.Album,
        updated.FileName ?? original.FileName,
        updated.FileSize ?? original.FileSize,
        updated.Bitrate ?? original.Bitrate,
        updated.UploadedAt ?? original.UploadedAt,
        updated.ArtworkUrl ?? original.ArtworkUrl,
        updated.DurationMilliseconds ?? original.DurationMilliseconds);

    private static string FormatSize(long? bytes)
    {
        if (bytes is null)
        {
            return "—";
        }

        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var value = (double)bytes.Value;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:0.##} {units[unit]}";
    }
}
