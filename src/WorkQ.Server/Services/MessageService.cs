using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkQ.Server.Data;
using WorkQ.Server.Dtos;
using WorkQ.Server.Models;
using WorkQ.Server.SignalR;

namespace WorkQ.Server.Services;

public sealed class MessageService(
    AppDbContext db,
    IHubContext<ChatHub, IChatClient> hub)
{
    public async Task<SendMessageResultDto> SendAsync(
        long senderId,
        Guid channelId,
        string content,
        string? clientId,
        CancellationToken cancellationToken)
    {
        var normalized = content.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            throw ApiException.BadRequest("INVALID_CONTENT", "Message content cannot be empty.");
        }

        if (normalized.Length > 4000)
        {
            throw ApiException.BadRequest("CONTENT_TOO_LONG", "Message content cannot exceed 4000 characters.");
        }

        var isMember = await db.ChannelMembers
            .AnyAsync(m => m.ChannelId == channelId && m.UserId == senderId, cancellationToken);
        if (!isMember)
        {
            throw ApiException.NotFound("CHANNEL_NOT_FOUND", "Channel not found or you are not a member.");
        }

        if (!string.IsNullOrWhiteSpace(clientId))
        {
            var existing = await db.Messages
                .Include(m => m.Sender)
                .FirstOrDefaultAsync(
                    m => m.ChannelId == channelId
                        && m.SenderId == senderId
                        && m.ClientId == clientId,
                    cancellationToken);

            if (existing is not null)
            {
                return new SendMessageResultDto(existing.ToDto(), Duplicate: true);
            }
        }

        var sender = await db.Users.FindAsync([senderId], cancellationToken);
        if (sender is null)
        {
            throw ApiException.NotFound("USER_NOT_FOUND", "Sender no longer exists.");
        }

        var message = new Message
        {
            ChannelId = channelId,
            SenderId = senderId,
            Sender = sender,
            Content = normalized,
            ClientId = string.IsNullOrWhiteSpace(clientId) ? null : clientId
        };

        db.Messages.Add(message);
        await db.SaveChangesAsync(cancellationToken);

        var dto = message.ToDto();
        await hub.Clients.Group($"channel:{channelId}").ReceiveMessage(dto);

        return new SendMessageResultDto(dto, Duplicate: false);
    }
}
