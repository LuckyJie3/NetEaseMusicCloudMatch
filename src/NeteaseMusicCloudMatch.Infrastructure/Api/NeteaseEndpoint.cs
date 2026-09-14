namespace NeteaseMusicCloudMatch.Infrastructure.Api;

public enum NeteaseApiHost
{
    Main,
    Interface
}

public sealed record NeteaseEndpoint(
    string Name,
    HttpMethod Method,
    string Path,
    bool IsWriteOperation = false,
    NeteaseApiHost Host = NeteaseApiHost.Main);
