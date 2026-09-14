namespace NeteaseMusicCloudMatch.App.Services;

public enum ApplicationLogSeverity
{
    Information,
    Warning,
    Error
}

public sealed record ApplicationLogEntry(
    DateTimeOffset Timestamp,
    ApplicationLogSeverity Severity,
    string Title,
    string Summary,
    string TechnicalDetails,
    string? ResponseJson = null,
    bool IsExpanded = false)
{
    public string TimeText => Timestamp.ToLocalTime().ToString("MM-dd HH:mm:ss");

    public bool HasResponseJson => !string.IsNullOrWhiteSpace(ResponseJson);
}
