namespace NeteaseMusicCloudMatch.Core.Models;

public sealed record SongDetails(
    long SongId,
    string? Name,
    string? Artists,
    string? Album,
    Uri? ArtworkUrl,
    long? DurationMilliseconds);
