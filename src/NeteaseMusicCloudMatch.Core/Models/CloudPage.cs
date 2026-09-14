namespace NeteaseMusicCloudMatch.Core.Models;

public sealed record CloudPage(
    int? TotalCount,
    long? UsedSize,
    long? MaxSize,
    IReadOnlyList<CloudSong> Songs,
    int Limit,
    int Offset);
