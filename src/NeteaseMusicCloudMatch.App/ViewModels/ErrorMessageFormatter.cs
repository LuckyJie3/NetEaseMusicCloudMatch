using NeteaseMusicCloudMatch.Core.Exceptions;

namespace NeteaseMusicCloudMatch.App.ViewModels;

internal static class ErrorMessageFormatter
{
    public static string Format(Exception exception) => exception switch
    {
        NeteaseApiException apiException => $"{apiException.Message}（诊断编号：{apiException.CorrelationId}）",
        FormatException formatException => formatException.Message,
        OperationCanceledException => "操作已取消。",
        _ => "操作失败，请查看脱敏日志获取诊断信息。"
    };
}
