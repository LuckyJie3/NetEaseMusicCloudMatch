namespace NeteaseMusicCloudMatch.App.Services;

public interface IUserDialogService
{
    Task<bool> ConfirmAsync(string message, string title, CancellationToken cancellationToken = default);

    void ShowSuccess(string message, string title = "操作完成");

    void ShowError(string message, string title = "操作失败");
}
