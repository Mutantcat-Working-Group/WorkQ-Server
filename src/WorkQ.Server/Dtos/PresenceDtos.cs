namespace WorkQ.Server.Dtos;

public sealed record UserPresenceDto(long UserId, bool IsOnline);

public sealed record UserTypingDto(Guid ChannelId, long UserId, string Username);
