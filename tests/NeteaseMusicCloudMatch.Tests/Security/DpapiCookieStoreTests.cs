using System.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using NeteaseMusicCloudMatch.Infrastructure.Authentication;

namespace NeteaseMusicCloudMatch.Tests.Security;

public sealed class DpapiCookieStoreTests
{
    [Fact]
    public async Task SaveLoadDelete_UsesCiphertextInsteadOfPlainCookieOnWindows()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var directory = Path.Combine(Path.GetTempPath(), "NeteaseMusicCloudMatch.Tests", Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "session.dat");
        const string cookie = "MUSIC_U=top-secret-test; __csrf=csrf-test";
        var store = new DpapiCookieStore(NullLogger<DpapiCookieStore>.Instance, path);

        try
        {
            try
            {
                await store.SaveAsync(cookie);
            }
            catch (CryptographicException)
            {
                // 某些隔离 CI 账户没有已加载的 Windows 用户配置文件，CurrentUser DPAPI 不可用。
                // 即便如此也必须保证失败路径没有把明文 Cookie 写入目标文件。
                Assert.False(File.Exists(path));
                return;
            }

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.DoesNotContain("top-secret-test", Encoding.UTF8.GetString(bytes), StringComparison.Ordinal);
            Assert.Equal(cookie, await store.LoadAsync());

            await store.DeleteAsync();
            Assert.False(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
