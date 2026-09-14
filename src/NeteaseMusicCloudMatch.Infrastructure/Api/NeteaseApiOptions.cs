namespace NeteaseMusicCloudMatch.Infrastructure.Api;

public sealed class NeteaseApiOptions
{
    public const string SectionName = "NeteaseApi";

    public Uri BaseUri { get; set; } = new("https://music.163.com", UriKind.Absolute);

    public Uri InterfaceBaseUri { get; set; } = new("https://interface.music.163.com", UriKind.Absolute);

    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    public int MaximumReadAttempts { get; set; } = 3;

    public ISet<string> AllowedCookieHosts { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "music.163.com",
        "interface.music.163.com"
    };
}
