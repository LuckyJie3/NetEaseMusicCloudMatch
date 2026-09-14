using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NeteaseMusicCloudMatch.App.Services;

namespace NeteaseMusicCloudMatch.App.ViewModels;

public sealed partial class LogViewModel : ObservableObject
{
    private readonly IApplicationLogReader _reader;
    private readonly IExternalNavigationService _navigation;
    private readonly IUserDialogService _dialogs;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RefreshCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearLogsCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private string _statusMessage = "显示本次运行的最近操作。";

    public LogViewModel(
        IApplicationLogReader reader,
        IExternalNavigationService navigation,
        IUserDialogService dialogs)
    {
        _reader = reader;
        _navigation = navigation;
        _dialogs = dialogs;
    }

    public ObservableCollection<ApplicationLogEntry> Entries { get; } = [];

    public string LogDirectory => AppPaths.LogDirectory;

    private bool CanChangeLogs() => !IsBusy;

    [RelayCommand(CanExecute = nameof(CanChangeLogs))]
    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var entries = await _reader.ReadRecentAsync(160, cancellationToken);
            Entries.Clear();
            foreach (var entry in entries)
            {
                Entries.Add(entry);
            }

            StatusMessage = Entries.Count == 0
                ? "还没有可显示的操作记录。"
                : $"共 {Entries.Count} 条，最新操作显示在最前。";
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            Entries.Clear();
            StatusMessage = "暂时无法读取日志文件。";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenLogDirectory() => _navigation.OpenDirectory(AppPaths.LogDirectory);

    [RelayCommand(CanExecute = nameof(CanChangeLogs))]
    private async Task ClearLogsAsync(CancellationToken cancellationToken)
    {
        if (IsBusy || !await _dialogs.ConfirmAsync(
                "确定要清除本机保存的应用日志吗？此操作不会删除登录状态或修改云盘歌曲。",
                "清除日志",
                cancellationToken))
        {
            return;
        }

        IsBusy = true;
        try
        {
            await _reader.ClearAsync(cancellationToken);
            Entries.Clear();
            StatusMessage = "日志已清除。后续操作会继续产生新的脱敏日志。";
            _dialogs.ShowSuccess("本机应用日志已清除。", "清除完成");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            StatusMessage = "日志文件正在被其他程序占用，暂时无法清除。";
            _dialogs.ShowError(StatusMessage, "清除失败");
        }
        finally
        {
            IsBusy = false;
        }
    }
}
