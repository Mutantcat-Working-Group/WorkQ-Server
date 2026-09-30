using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorkQ.Server.Data;
using WorkQ.Server.Dtos;
using WorkQ.Server.Extensions;
using WorkQ.Server.Services;

namespace WorkQ.Server.SignalR;

[Authorize]
public sealed class ChatHub(
    AppDbContext db,
    PresenceService presence,
    MessageService messages) : Hub<IChatClient>
{
    public override async Task OnConnectedAsync()
    {
        var userId = Context.User!.GetUserId();
        var isFirstConnection = presence.MarkOnline(userId);

        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{userId}");

        if (isFirstConnection)
        {
            await NotifyPeersAsync(userId, new UserPresenceDto(userId, true), Context.ConnectionAborted);
        }

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.User!.GetUserId();
        var wasLastConnection = presence.MarkOffline(userId);

        if (wasLastConnection)
        {
            await NotifyPeersAsync(userId, new UserPresenceDto(userId, false), CancellationToken.None);
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinChannel(Guid channelId)
    {
        var userId = Context.User!.GetUserId();
        await EnsureMemberAsync(userId, channelId, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId, $"channel:{channelId}");
        await Clients.Caller.JoinedChannel(channelId);
    }

    public async Task LeaveChannel(Guid channelId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"channel:{channelId}");
        await Clients.Caller.LeftChannel(channelId);
    }

    public async Task<SendMessageResultDto> SendMessage(
        Guid channelId,
        string content,
        string? clientId = null)
    {
        var result = await messages.SendAsync(
            Context.User!.GetUserId(),
            channelId,
            content,
            clientId,
            Context.ConnectionAborted);

        await Clients.Caller.MessageSent(result);
        return result;
    }

    public async Task Typing(Guid channelId)
    {
        var userId = Context.User!.GetUserId();
        await EnsureMemberAsync(userId, channelId, Context.ConnectionAborted);

        await Clients.GroupExcept($"channel:{channelId}", Context.ConnectionId)
            .UserTyping(new UserTypingDto(channelId, userId, Context.User!.GetUsername()));
    }

    public async Task<IReadOnlyList<long>> GetChannelOnlineUsers(Guid channelId)
    {
        var userId = Context.User!.GetUserId();
        await EnsureMemberAsync(userId, channelId, Context.ConnectionAborted);

        var memberIds = await db.ChannelMembers
            .Where(m => m.ChannelId == channelId)
            .Select(m => m.UserId)
            .ToListAsync(Context.ConnectionAborted);

        return memberIds.Where(presence.IsOnline).ToList();
    }

    private async Task EnsureMemberAsync(long userId, Guid channelId, CancellationToken cancellationToken)
    {
        var isMember = await db.ChannelMembers.AnyAsync(
            m => m.ChannelId == channelId && m.UserId == userId,
            cancellationToken);

        if (!isMember)
        {
            throw new HubException("CHANNEL_NOT_FOUND");
        }
    }

    private async Task NotifyPeersAsync(
        long userId,
        UserPresenceDto presenceDto,
        CancellationToken cancellationToken)
    {
        var channelIds = await db.ChannelMembers
            .Where(m => m.UserId == userId)
            .Select(m => m.ChannelId)
            .ToListAsync(cancellationToken);

        if (channelIds.Count == 0)
        {
            return;
        }

        var peerIds = await db.ChannelMembers
            .Where(m => channelIds.Contains(m.ChannelId) && m.UserId != userId)
            .Select(m => m.UserId)
            .Distinct()
            .ToListAsync(cancellationToken);

        foreach (var peerId in peerIds.Where(presence.IsOnline))
        {
            await Clients.Group($"user:{peerId}").UserPresenceChanged(presenceDto);
        }
    }
}
