namespace NeteaseMusicCloudMatch.Core.Models;

public sealed record CloudSong(
    int SourceIndex,
    long? SongId,
    long? CurrentMatchedSongId,
    string? Name,
    string? Artists,
    string? Album,
    string? FileName,
    long? FileSize,
    int? Bitrate,
    DateTimeOffset? UploadedAt,
    Uri? ArtworkUrl,
    long? DurationMilliseconds);
