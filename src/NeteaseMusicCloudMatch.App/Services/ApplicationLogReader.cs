using System.Globalization;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using NeteaseMusicCloudMatch.Core.Security;

namespace NeteaseMusicCloudMatch.App.Services;

public interface IApplicationLogReader
{
    Task<IReadOnlyList<ApplicationLogEntry>> ReadRecentAsync(
        int maximumEntries,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}

public sealed partial class ApplicationLogReader : IApplicationLogReader
{
    private const string ResponseMarker = " ResponseJson=";
    private static readonly JsonSerializerOptions PrettyJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };
    private readonly string _logDirectory;

    public ApplicationLogReader()
        : this(AppPaths.LogDirectory)
    {
    }

    internal ApplicationLogReader(string logDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(logDirectory);
        _logDirectory = Path.GetFullPath(logDirectory);
    }

    public async Task<IReadOnlyList<ApplicationLogEntry>> ReadRecentAsync(
        int maximumEntries,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumEntries, 1);
        Directory.CreateDirectory(_logDirectory);
        var file = Directory.EnumerateFiles(_logDirectory, "*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault();
        if (file is null)
        {
            return [];
        }

        var queue = new Queue<ApplicationLogEntry>(maximumEntries);
        await using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, true);
        using var reader = new StreamReader(stream);
        while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } rawLine)
        {
            var line = DiagnosticSanitizer.Redact(rawLine);
            var entry = Parse(line);
            if (entry is null)
            {
                continue;
            }

            if (queue.Count == maximumEntries)
            {
                queue.Dequeue();
            }

            queue.Enqueue(entry);
        }

        return queue.Reverse().ToArray();
    }

    public async Task ClearAsync(CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_logDirectory);
        foreach (var file in Directory.EnumerateFiles(_logDirectory, "*.log"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var stream = new FileStream(
                    file,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete,
                    4096,
                    FileOptions.Asynchronous);
                stream.SetLength(0);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (FileNotFoundException)
            {
                // A rolling log may disappear between enumeration and opening.
            }
        }
    }

    internal static ApplicationLogEntry? Parse(string line)
    {
        var match = LogLineRegex().Match(line);
        if (!match.Success ||
            !DateTimeOffset.TryParse(match.Groups["time"].Value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
        {
            return null;
        }

        var level = match.Groups["level"].Value;
        var (message, responseJson) = SplitResponseJson(match.Groups["message"].Value);
        if (message.StartsWith("Hosting environment:", StringComparison.Ordinal) ||
            message.StartsWith("Content root path:", StringComparison.Ordinal) ||
            message.StartsWith("UI operation failed safely.", StringComparison.Ordinal))
        {
            return null;
        }

        var severity = level switch
        {
            "ERR" or "FTL" => ApplicationLogSeverity.Error,
            "WRN" => ApplicationLogSeverity.Warning,
            _ => ApplicationLogSeverity.Information
        };

        if (message.StartsWith("Netease request completed.", StringComparison.Ordinal))
        {
            var operation = Field(message, "Operation") ?? "NeteaseRequest";
            var elapsed = Field(message, "ElapsedMs");
            return new ApplicationLogEntry(
                timestamp,
                severity,
                FriendlyOperation(operation),
                elapsed is null ? "请求已完成" : $"请求成功 · {elapsed} ms",
                BuildTechnicalDetails(message),
                responseJson);
        }

        if (message.StartsWith("Netease request failed.", StringComparison.Ordinal))
        {
            var operation = Field(message, "Operation") ?? "NeteaseRequest";
            var diagnostic = QuotedField(message, "Diagnostic") ?? "网易云接口返回失败";
            return new ApplicationLogEntry(
                timestamp,
                ApplicationLogSeverity.Error,
                FriendlyOperation(operation),
                diagnostic,
                BuildTechnicalDetails(message),
                responseJson);
        }

        if (message.StartsWith("Application started.", StringComparison.Ordinal))
        {
            return new ApplicationLogEntry(timestamp, severity, "应用已启动", "云盘匹配已准备就绪", "本次运行开始");
        }

        if (message.StartsWith("Encrypted login session saved", StringComparison.Ordinal))
        {
            return new ApplicationLogEntry(
                timestamp,
                severity,
                "登录状态已保存",
                "Cookie 已通过 Windows DPAPI 加密保存",
                "仅当前 Windows 用户可以解密 session.dat");
        }

        if (message.StartsWith("Cookie login failed safely.", StringComparison.Ordinal))
        {
            var errorType = Field(message, "ErrorType");
            var summary = errorType == "FormatException" ? "粘贴的 Cookie 格式不正确" : "Cookie 验证未通过";
            return new ApplicationLogEntry(timestamp, ApplicationLogSeverity.Warning, "Cookie 登录", summary, $"错误类型：{errorType ?? "Unknown"}");
        }

        if (message.StartsWith("Cloud page load failed safely.", StringComparison.Ordinal))
        {
            return new ApplicationLogEntry(timestamp, ApplicationLogSeverity.Warning, "加载云盘", "云盘列表加载失败", message);
        }

        return new ApplicationLogEntry(timestamp, severity, "应用事件", FriendlyFallback(message), message);
    }

    private static string FriendlyOperation(string operation) => operation switch
    {
        "GetAccount" => "验证登录",
        "GetCloudSongs" => "加载云盘",
        "GetCloudSongById" => "校验云盘歌曲",
        "GetSongDetail" => "查询目标歌曲",
        "MatchCloudSong" => "匹配歌曲",
        "CancelCloudMatch" => "取消匹配",
        _ => "网易云请求"
    };

    private static string BuildTechnicalDetails(string message)
    {
        var method = Field(message, "Method");
        var endpoint = Field(message, "Endpoint");
        var http = Field(message, "HttpStatus");
        var api = Field(message, "ApiCode");
        var correlation = Field(message, "CorrelationId");
        var parts = new List<string>();
        if (method is not null || endpoint is not null)
        {
            parts.Add($"{method ?? "—"} {endpoint ?? "—"}");
        }

        if (http is not null)
        {
            parts.Add($"HTTP {http}");
        }

        if (api is not null)
        {
            parts.Add($"API {api}");
        }

        if (correlation is not null)
        {
            parts.Add($"追踪号 {correlation}");
        }

        return parts.Count == 0 ? message : string.Join("  ·  ", parts);
    }

    private static string FriendlyFallback(string message) =>
        message.Length <= 90 ? message : string.Concat(message.AsSpan(0, 90), "…");

    private static (string Message, string? ResponseJson) SplitResponseJson(string message)
    {
        var markerIndex = message.IndexOf(ResponseMarker, StringComparison.Ordinal);
        if (markerIndex < 0)
        {
            return (message, null);
        }

        var rawJson = DiagnosticSanitizer.Redact(message[(markerIndex + ResponseMarker.Length)..].Trim());
        if (string.IsNullOrWhiteSpace(rawJson) || string.Equals(rawJson, "null", StringComparison.OrdinalIgnoreCase))
        {
            return (message[..markerIndex], null);
        }

        try
        {
            using var document = JsonDocument.Parse(rawJson);
            return (
                message[..markerIndex],
                JsonSerializer.Serialize(document.RootElement, PrettyJsonOptions));
        }
        catch (JsonException)
        {
            return (message[..markerIndex], rawJson);
        }
    }

    private static string? Field(string message, string name)
    {
        var match = Regex.Match(message, $@"(?:^|\s){Regex.Escape(name)}=(?<value>[^\s]+)");
        return match.Success ? match.Groups["value"].Value.Trim('"') : null;
    }

    private static string? QuotedField(string message, string name)
    {
        var pattern = "(?:^|\\s)" + Regex.Escape(name) + "=\"(?<value>[^\"]*)\"";
        var match = Regex.Match(message, pattern);
        return match.Success ? match.Groups["value"].Value : Field(message, name);
    }

    [GeneratedRegex(@"^(?<time>\d{4}-\d{2}-\d{2} \d{2}:\d{2}:\d{2}\.\d{3} [+-]\d{2}:\d{2}) \[(?<level>[A-Z]{3})\] (?<message>.*)$")]
    private static partial Regex LogLineRegex();
}
