using Wpf.Ui;
using Wpf.Ui.Controls;

namespace NeteaseMusicCloudMatch.App.Services;

public sealed class UserDialogService(
    IContentDialogService contentDialogs,
    ISnackbarService snackbars) : IUserDialogService
{
    public async Task<bool> ConfirmAsync(
        string message,
        string title,
        CancellationToken cancellationToken = default)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            PrimaryButtonText = "确认",
            CloseButtonText = "返回",
            PrimaryButtonAppearance = ControlAppearance.Primary,
            CloseButtonAppearance = ControlAppearance.Secondary
        };

        var result = await contentDialogs.ShowAsync(dialog, cancellationToken);
        return result == ContentDialogResult.Primary;
    }

    public void ShowSuccess(string message, string title = "操作完成") =>
        snackbars.Show(title, message, ControlAppearance.Success, null, TimeSpan.FromSeconds(4));

    public void ShowError(string message, string title = "操作失败") =>
        snackbars.Show(title, message, ControlAppearance.Danger, null, TimeSpan.FromSeconds(6));
}
