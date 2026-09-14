namespace NeteaseMusicCloudMatch.App.Services;

public interface IExternalNavigationService
{
    void OpenUrl(Uri uri);

    void OpenDirectory(string path);
}
