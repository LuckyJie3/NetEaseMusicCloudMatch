using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using NeteaseMusicCloudMatch.Core.Services;

namespace NeteaseMusicCloudMatch.Infrastructure.Authentication;

public sealed class DpapiCookieStore : ICookieStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("NeteaseMusicCloudMatch.Session.v1");
    private readonly ILogger<DpapiCookieStore> _logger;

    public DpapiCookieStore(ILogger<DpapiCookieStore> logger, string? sessionPath = null)
    {
        _logger = logger;
        SessionPath = sessionPath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NeteaseMusicCloudMatch",
            "session.dat");
    }

    public string SessionPath { get; }

    public async Task SaveAsync(string cookie, CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI Cookie 存储仅支持 Windows。");
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(cookie);
        cancellationToken.ThrowIfCancellationRequested();

        var directory = Path.GetDirectoryName(SessionPath)!;
        Directory.CreateDirectory(directory);
        var plaintext = Encoding.UTF8.GetBytes(cookie);
        byte[] protectedData;
        try
        {
            protectedData = ProtectedData.Protect(plaintext, Entropy, DataProtectionScope.CurrentUser);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }

        var temporaryPath = SessionPath + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporaryPath, protectedData, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, SessionPath, true);
            _logger.LogInformation("Encrypted login session saved with Windows DPAPI.");
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedData);
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public async Task<string?> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("DPAPI Cookie 存储仅支持 Windows。");
        }
        if (!File.Exists(SessionPath))
        {
            return null;
        }

        var protectedData = await File.ReadAllBytesAsync(SessionPath, cancellationToken).ConfigureAwait(false);
        byte[]? plaintext = null;
        try
        {
            plaintext = ProtectedData.Unprotect(protectedData, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plaintext);
        }
        catch (CryptographicException)
        {
            _logger.LogWarning("Encrypted login session could not be decrypted and will be removed.");
            await DeleteAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(protectedData);
            if (plaintext is not null)
            {
                CryptographicOperations.ZeroMemory(plaintext);
            }
        }
    }

    public Task DeleteAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (File.Exists(SessionPath))
        {
            File.Delete(SessionPath);
            _logger.LogInformation("Encrypted login session removed.");
        }

        return Task.CompletedTask;
    }

}
