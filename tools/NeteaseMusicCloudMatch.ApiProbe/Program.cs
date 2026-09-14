using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NeteaseMusicCloudMatch.Core.Exceptions;
using NeteaseMusicCloudMatch.Core.Models;
using NeteaseMusicCloudMatch.Core.Security;
using NeteaseMusicCloudMatch.Core.Services;
using NeteaseMusicCloudMatch.Infrastructure.DependencyInjection;

const string CookieEnvironmentVariable = "NETEASE_MUSIC_COOKIE";
const string WriteConfirmation = "I_UNDERSTAND";

var cookie = Environment.GetEnvironmentVariable(CookieEnvironmentVariable);
if (string.IsNullOrWhiteSpace(cookie))
{
    Console.Error.WriteLine($"未设置环境变量 {CookieEnvironmentVariable}。Probe 不从源码或配置文件读取 Cookie。");
    return 2;
}

try
{
    cookie = CookieParser.Parse(cookie).HeaderValue;
}
catch (FormatException)
{
    Console.Error.WriteLine("环境变量中的 Cookie 格式无效；出于安全原因不会回显原值。");
    return 2;
}

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Services.AddNeteaseMusicCloudMatchInfrastructure();

using var host = builder.Build();
var api = host.Services.GetRequiredService<INeteaseApiClient>();

try
{
    var account = await api.GetAccountAsync(cookie);
    PrintResult("GetAccount", account, account.Value is null
        ? "profile=missing"
        : $"userId={account.Value.UserId}; nickname={Safe(account.Value.Nickname)}");

    if (account.Value is null)
    {
        Console.Error.WriteLine("Cookie 未返回有效 profile，停止 Probe。");
        return 3;
    }

    var cloud = await api.GetCloudSongsAsync(cookie, 200, 0);
    PrintResult(
        "GetCloudSongs",
        cloud,
        $"count={Display(cloud.Value.TotalCount)}; pageItems={cloud.Value.Songs.Count}; size={Display(cloud.Value.UsedSize)}; maxSize={Display(cloud.Value.MaxSize)}");

    var cloudSongId = GetLongOption(args, "--cloud-song-id") ?? cloud.Value.Songs.FirstOrDefault(song => song.SongId is > 0)?.SongId;
    if (cloudSongId is > 0)
    {
        var cloudSong = await api.GetCloudSongByIdAsync(cookie, cloudSongId.Value);
        PrintResult(
            "GetCloudSongById",
            cloudSong,
            cloudSong.Value is null
                ? $"cloudSongId={cloudSongId}; found=false"
                : $"cloudSongId={cloudSongId}; found=true; name={Safe(cloudSong.Value.Name)}");
    }
    else
    {
        Console.WriteLine("GetCloudSongById: SKIPPED（云盘页中没有有效 songId，且未提供 --cloud-song-id）");
    }

    var targetSongId = GetLongOption(args, "--song-id") ?? 186137;
    var song = await api.GetSongDetailAsync(cookie, targetSongId);
    PrintResult(
        "GetSongDetail",
        song,
        song.Value is null
            ? $"songId={targetSongId}; found=false"
            : $"songId={song.Value.SongId}; name={Safe(song.Value.Name)}; artists={Safe(song.Value.Artists)}");

    var wantsMatch = args.Contains("--match", StringComparer.OrdinalIgnoreCase);
    var wantsCancel = args.Contains("--cancel", StringComparer.OrdinalIgnoreCase);
    if (wantsMatch || wantsCancel)
    {
        var confirmation = GetStringOption(args, "--confirm-write");
        if (!string.Equals(confirmation, WriteConfirmation, StringComparison.Ordinal))
        {
            Console.Error.WriteLine($"写 Probe 未执行：必须显式提供 --confirm-write={WriteConfirmation}。");
            return 4;
        }

        var explicitCloudSongId = GetLongOption(args, "--cloud-song-id");
        if (explicitCloudSongId is not > 0)
        {
            Console.Error.WriteLine("写 Probe 未执行：必须显式提供正整数 --cloud-song-id。");
            return 4;
        }

        var explicitTargetId = wantsCancel ? 0 : GetLongOption(args, "--target-song-id");
        if (explicitTargetId is null || explicitTargetId < 0 || (!wantsCancel && explicitTargetId == 0))
        {
            Console.Error.WriteLine("写 Probe 未执行：Match 必须提供正整数 --target-song-id；Cancel 会固定使用 0。");
            return 4;
        }

        Console.WriteLine(
            $"执行显式写 Probe：userId={account.Value.UserId}; songId={explicitCloudSongId}; adjustSongId={explicitTargetId}");
        var match = await api.MatchCloudSongAsync(
            cookie,
            account.Value.UserId,
            explicitCloudSongId.Value,
            explicitTargetId.Value);
        PrintResult(
            wantsCancel ? "CancelCloudMatch" : "MatchCloudSong",
            match,
            $"succeeded={match.Value.Succeeded}; matchData={(match.Value.UpdatedSong is null ? "absent" : "present")}");
    }

    return 0;
}
catch (NeteaseApiException exception)
{
    Console.Error.WriteLine(
        $"Probe failed: kind={exception.Kind}; operation={exception.Operation}; http={Display(exception.HttpStatus is null ? null : (int)exception.HttpStatus)}; apiCode={Display(exception.ApiCode)}; correlationId={exception.CorrelationId}; message={Safe(exception.Message)}");
    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Probe failed safely: errorType={exception.GetType().Name}; message={Safe(exception.Message)}");
    return 1;
}

static void PrintResult<T>(string operation, ApiCallResult<T> result, string fields) =>
    Console.WriteLine(
        $"{operation}: HTTP={(int)result.HttpStatus}; apiCode={Display(result.ApiCode)}; elapsedMs={result.Elapsed.TotalMilliseconds:F0}; correlationId={result.CorrelationId}; {DiagnosticSanitizer.Redact(fields)}");

static string Safe(string? value) =>
    string.IsNullOrWhiteSpace(value) ? "—" : DiagnosticSanitizer.Redact(value.Replace('\r', ' ').Replace('\n', ' '));

static string Display(object? value) => value?.ToString() ?? "—";

static long? GetLongOption(string[] arguments, string name)
{
    var value = GetStringOption(arguments, name);
    return long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
}

static string? GetStringOption(string[] arguments, string name)
{
    var prefix = name + "=";
    var inline = arguments.FirstOrDefault(argument => argument.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    if (inline is not null)
    {
        return inline[prefix.Length..];
    }

    var index = Array.FindIndex(arguments, argument => argument.Equals(name, StringComparison.OrdinalIgnoreCase));
    return index >= 0 && index + 1 < arguments.Length ? arguments[index + 1] : null;
}
