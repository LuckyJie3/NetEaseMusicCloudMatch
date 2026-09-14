using System.Diagnostics;
using System.IO;

namespace NeteaseMusicCloudMatch.App.Services;

public sealed class ExternalNavigationService : IExternalNavigationService
{
    public void OpenUrl(Uri uri)
    {
        if (uri.Scheme is not ("https" or "http"))
        {
            throw new InvalidOperationException("只允许打开 HTTP/HTTPS 链接。");
        }

        Process.Start(new ProcessStartInfo(uri.AbsoluteUri) { UseShellExecute = true });
    }

    public void OpenDirectory(string path)
    {
        Directory.CreateDirectory(path);
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
    }
}
