using WorkQ.Server.Dtos;
using WorkQ.Server.Models;

namespace WorkQ.Server.Services;

public static class DtoMapper
{
    public static UserDto ToDto(this User user) =>
        new(user.Id, user.Username, user.DisplayName, user.CreatedAtUtc);

    public static MessageDto ToDto(this Message message) =>
        new(
            message.Id,
            message.ChannelId,
            message.SenderId,
            message.Sender?.Username ?? string.Empty,
            message.Sender?.DisplayName ?? string.Empty,
            message.Content,
            message.ClientId,
            message.SentAtUtc);
}
