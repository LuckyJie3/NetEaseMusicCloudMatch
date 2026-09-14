using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NeteaseMusicCloudMatch.App.Services;
using NeteaseMusicCloudMatch.Core.Models;
using NeteaseMusicCloudMatch.Core.Parsing;
using NeteaseMusicCloudMatch.Core.Services;

namespace NeteaseMusicCloudMatch.App.ViewModels;

public sealed partial class MatchViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICloudDriveService _cloudDriveService;
    private readonly ISongService _songService;
    private readonly ICloudMatchService _matchService;
    private readonly IUserDialogService _dialogs;
    private readonly ILogger<MatchViewModel> _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewTargetCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelMatchCommand))]
    private CloudSongRowViewModel? _selectedCloudSong;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewTargetCommand))]
    private string _targetInput = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ConfirmMatchCommand))]
    private SongDetails? _targetPreview;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(PreviewTargetCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmMatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelMatchCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "请先从左侧选择一首云盘歌曲。";

    public MatchViewModel(
        IAuthService authService,
        ICloudDriveService cloudDriveService,
        ISongService songService,
        ICloudMatchService matchService,
        IUserDialogService dialogs,
        ILogger<MatchViewModel> logger)
    {
        _authService = authService;
        _cloudDriveService = cloudDriveService;
        _songService = songService;
        _matchService = matchService;
        _dialogs = dialogs;
        _logger = logger;
    }

    public event EventHandler<MatchCompletedEventArgs>? MatchCompleted;

    partial void OnSelectedCloudSongChanged(CloudSongRowViewModel? value)
    {
        TargetInput = string.Empty;
        TargetPreview = null;
        StatusMessage = value is null ? "请先从左侧选择一首云盘歌曲。" : "请输入目标歌曲 ID 或单曲链接并预览。";
    }

    partial void OnTargetInputChanged(string value)
    {
        TargetPreview = null;
        ConfirmMatchCommand.NotifyCanExecuteChanged();
    }

    private bool CanPreviewTarget() =>
        !IsBusy && SelectedCloudSong?.Model.SongId is > 0 && !string.IsNullOrWhiteSpace(TargetInput);

    [RelayCommand(CanExecute = nameof(CanPreviewTarget))]
    private async Task PreviewTargetAsync(CancellationToken cancellationToken)
    {
        var parsed = SongIdParser.Parse(TargetInput);
        if (!parsed.Success || parsed.SongId is null)
        {
            StatusMessage = parsed.Error ?? "无法解析歌曲 ID。";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在查询目标歌曲…";
        try
        {
            TargetPreview = await _songService.GetDetailsAsync(parsed.SongId.Value, cancellationToken);
            StatusMessage = "请核对歌曲信息，确认无误后再匹配。";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            TargetPreview = null;
            StatusMessage = ErrorMessageFormatter.Format(exception);
            LogFailure("PreviewTarget", exception);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanConfirmMatch() =>
        !IsBusy && SelectedCloudSong?.Model.SongId is > 0 && TargetPreview is not null;

    [RelayCommand(CanExecute = nameof(CanConfirmMatch))]
    private async Task ConfirmMatchAsync(CancellationToken cancellationToken)
    {
        if (SelectedCloudSong?.Model.SongId is not { } cloudSongId || TargetPreview is not { } target)
        {
            return;
        }

        if (SelectedCloudSong.Model.CurrentMatchedSongId == target.SongId)
        {
            StatusMessage = "当前云盘歌曲已经关联到这个目标歌曲。";
            return;
        }

        if (!await _dialogs.ConfirmAsync(
                $"确定要把《{SelectedCloudSong.Name}》关联到《{target.Name ?? "未知歌曲"}》（ID {target.SongId}）吗？",
                "确认匹配",
                cancellationToken))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在执行匹配，请勿重复点击…";
        try
        {
            await _cloudDriveService.GetByIdAsync(cloudSongId, cancellationToken);
            var userId = _authService.CurrentUser?.UserId ?? throw new InvalidOperationException("登录状态已失效。");
            var result = await _matchService.MatchAsync(userId, cloudSongId, target.SongId, cancellationToken);
            StatusMessage = "匹配成功。";
            MatchCompleted?.Invoke(this, new MatchCompletedEventArgs(cloudSongId, result.UpdatedSong));
            _dialogs.ShowSuccess("关联信息已更新，可以继续处理下一首歌曲。", "匹配成功");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusMessage = ErrorMessageFormatter.Format(exception);
            LogFailure("MatchCloudSong", exception);
            _dialogs.ShowError(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool CanCancelMatch() => !IsBusy && SelectedCloudSong?.Model.SongId is > 0;

    [RelayCommand(CanExecute = nameof(CanCancelMatch))]
    private async Task CancelMatchAsync(CancellationToken cancellationToken)
    {
        if (SelectedCloudSong?.Model.SongId is not { } cloudSongId)
        {
            return;
        }

        if (!await _dialogs.ConfirmAsync(
                "确定要取消这首云盘歌曲当前的网易云曲库关联吗？",
                "取消匹配",
                cancellationToken))
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "正在取消匹配，请勿重复点击…";
        try
        {
            await _cloudDriveService.GetByIdAsync(cloudSongId, cancellationToken);
            var userId = _authService.CurrentUser?.UserId ?? throw new InvalidOperationException("登录状态已失效。");
            var result = await _matchService.CancelAsync(userId, cloudSongId, cancellationToken);
            TargetInput = string.Empty;
            TargetPreview = null;
            StatusMessage = "已取消匹配。";
            MatchCompleted?.Invoke(this, new MatchCompletedEventArgs(cloudSongId, result.UpdatedSong));
            _dialogs.ShowSuccess("这首云盘歌曲已恢复为未关联状态。", "已取消匹配");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusMessage = ErrorMessageFormatter.Format(exception);
            LogFailure("CancelCloudMatch", exception);
            _dialogs.ShowError(StatusMessage);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void LogFailure(string operation, Exception exception) =>
        _logger.LogWarning(
            "UI operation failed safely. Operation={Operation} ErrorType={ErrorType}",
            operation,
            exception.GetType().Name);
}

public sealed record MatchCompletedEventArgs(long CloudSongId, CloudSong? UpdatedSong);
