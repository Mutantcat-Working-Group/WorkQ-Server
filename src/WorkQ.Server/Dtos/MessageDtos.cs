namespace WorkQ.Server.Dtos;

public sealed record SendMessageRequest(string Content, string? ClientId);

public sealed record MessageDto(
    long Id,
    Guid ChannelId,
    long SenderId,
    string SenderUsername,
    string SenderDisplayName,
    string Content,
    string? ClientId,
    DateTime SentAtUtc);

public sealed record SendMessageResultDto(MessageDto Message, bool Duplicate);

public sealed record MessagePageDto(
    IReadOnlyList<MessageDto> Messages,
    long? NextBefore);
