namespace NeteaseMusicCloudMatch.Core.Models;

public sealed record MatchResult(bool Succeeded, CloudSong? UpdatedSong);
