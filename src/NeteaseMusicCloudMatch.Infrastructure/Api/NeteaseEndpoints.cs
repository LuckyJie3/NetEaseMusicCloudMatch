namespace NeteaseMusicCloudMatch.Infrastructure.Api;

public static class NeteaseEndpoints
{
    public static readonly NeteaseEndpoint Account = new("GetAccount", HttpMethod.Post, "/api/nuser/account/get");

    public static readonly NeteaseEndpoint Cloud = new("GetCloudSongs", HttpMethod.Post, "/api/v1/cloud/get");

    public static readonly NeteaseEndpoint CloudByIds = new("GetCloudSongById", HttpMethod.Get, "/api/cloud/get/byids");

    public static readonly NeteaseEndpoint SongDetail = new("GetSongDetail", HttpMethod.Get, "/api/song/detail/");

    public static readonly NeteaseEndpoint CloudMatch = new(
        "MatchCloudSong",
        HttpMethod.Get,
        "/api/cloud/user/song/match",
        true,
        NeteaseApiHost.Interface);
}
