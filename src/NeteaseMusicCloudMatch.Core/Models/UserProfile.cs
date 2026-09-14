namespace NeteaseMusicCloudMatch.Core.Models;

public sealed record UserProfile(long UserId, string? Nickname, Uri? AvatarUrl);
