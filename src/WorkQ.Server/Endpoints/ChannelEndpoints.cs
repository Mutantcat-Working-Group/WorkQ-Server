using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using WorkQ.Server.Data;
using WorkQ.Server.Dtos;
using WorkQ.Server.Extensions;
using WorkQ.Server.Models;
using WorkQ.Server.Services;

namespace WorkQ.Server.Endpoints;

public static class ChannelEndpoints
{
    private const int MaxMembersPerChannel = 500;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public static RouteGroupBuilder MapChannelEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/channels")
            .WithTags("Channels")
            .RequireAuthorization();

        group.MapGet("/", ListChannelsAsync);
        group.MapPost("/", CreateChannelAsync);
        group.MapGet(
            "/{channelId:guid}",
            (Guid channelId, ClaimsPrincipal principal, AppDbContext db, CancellationToken cancellationToken) =>
                GetChannelAsync(channelId, principal, db, cancellationToken));
        group.MapPost("/{channelId:guid}/members", AddMembersAsync);
        group.MapPost("/{channelId:guid}/join", JoinChannelAsync);
        group.MapPost("/{channelId:guid}/leave", LeaveChannelAsync);
        group.MapGet("/{channelId:guid}/messages", GetMessagesAsync);
        group.MapPost("/{channelId:guid}/messages", SendMessageAsync);
        group.MapPost("/{channelId:guid}/read", MarkReadAsync);
        group.MapDelete("/{channelId:guid}", DeleteChannelAsync);

        return group;
    }

    private static async Task<IResult> ListChannelsAsync(
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var summaries = await db.ChannelMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.JoinedAtUtc)
            .Select(m => new ChannelSummaryDto(
                m.ChannelId,
                m.Channel!.Name,
                m.Channel.Kind,
                m.Channel.OwnerId,
                m.Channel.Members.Count,
                m.Channel.CreatedAtUtc,
                m.Channel.Messages
                    .OrderByDescending(message => message.SentAtUtc)
                    .Select(message => new MessageDto(
                        message.Id,
                        message.ChannelId,
                        message.SenderId,
                        message.Sender!.Username,
                        message.Sender.DisplayName,
                        message.Content,
                        message.ClientId,
                        message.SentAtUtc))
                    .FirstOrDefault(),
                m.Channel.Messages.Count(message =>
                    message.SenderId != userId
                    && (message.SentAtUtc > m.LastReadAtUtc || m.LastReadAtUtc == null)),
                m.LastReadAtUtc))
            .ToListAsync(cancellationToken);

        return Results.Ok(summaries);
    }

    private static async Task<IResult> CreateChannelAsync(
        CreateChannelRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();

        return request.Kind switch
        {
            ChannelKind.Direct => await CreateDirectChannelAsync(request, userId, db, cancellationToken),
            ChannelKind.Group => await CreateGroupChannelAsync(request, userId, db, cancellationToken),
            _ => throw ApiException.BadRequest("INVALID_KIND", "Channel kind must be direct or group.")
        };
    }

    private static async Task<IResult> CreateDirectChannelAsync(
        CreateChannelRequest request,
        long userId,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var otherId = request.MemberIds?.Count == 1 ? request.MemberIds[0] : 0;
        if (otherId == 0 || otherId == userId)
        {
            throw ApiException.BadRequest(
                "INVALID_MEMBERS",
                "A direct channel needs exactly one other user id.");
        }

        var otherUser = await db.Users
            .FirstOrDefaultAsync(u => u.Id == otherId, cancellationToken)
            ?? throw ApiException.NotFound("USER_NOT_FOUND", "The other user does not exist.");

        var myDirectChannels = await db.ChannelMembers
            .AsNoTracking()
            .Where(m => m.UserId == userId && m.Channel!.Kind == ChannelKind.Direct)
            .Include(m => m.Channel!.Members)
            .Select(m => m.Channel!)
            .ToListAsync(cancellationToken);

        var existing = myDirectChannels.FirstOrDefault(c =>
            c.Members.Count == 2 && c.Members.Any(m => m.UserId == otherId));
        if (existing is not null)
        {
            return Results.Ok(await GetChannelAsync(db, existing.Id, userId, cancellationToken));
        }

        var channel = new Channel
        {
            Name = $"{otherUser.Username}_{userId}",
            Kind = ChannelKind.Direct,
            OwnerId = userId
        };
        db.Channels.Add(channel);
        db.ChannelMembers.AddRange(
            new ChannelMember { ChannelId = channel.Id, UserId = userId },
            new ChannelMember { ChannelId = channel.Id, UserId = otherId });
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/channels/{channel.Id}",
            await GetChannelAsync(db, channel.Id, userId, cancellationToken));
    }

    private static async Task<IResult> CreateGroupChannelAsync(
        CreateChannelRequest request,
        long userId,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (name is null || name.Length is < 2 or > 64)
        {
            throw ApiException.BadRequest(
                "INVALID_NAME",
                "Group channel name must be between 2 and 64 characters.");
        }

        var requestedMemberIds = request.MemberIds?
            .Where(id => id != userId)
            .Distinct()
            .ToList() ?? [];
        if (requestedMemberIds.Count > MaxMembersPerChannel)
        {
            throw ApiException.BadRequest(
                "TOO_MANY_MEMBERS",
                $"A channel cannot have more than {MaxMembersPerChannel} members.");
        }

        var existingCount = requestedMemberIds.Count == 0
            ? 0
            : await db.Users.CountAsync(
                u => requestedMemberIds.Contains(u.Id),
                cancellationToken);
        if (existingCount != requestedMemberIds.Count)
        {
            throw ApiException.NotFound("USER_NOT_FOUND", "One or more member ids do not exist.");
        }

        var channel = new Channel
        {
            Name = name,
            Kind = ChannelKind.Group,
            OwnerId = userId
        };
        db.Channels.Add(channel);
        db.ChannelMembers.Add(new ChannelMember { ChannelId = channel.Id, UserId = userId });
        db.ChannelMembers.AddRange(requestedMemberIds.Select(memberId =>
            new ChannelMember { ChannelId = channel.Id, UserId = memberId }));
        await db.SaveChangesAsync(cancellationToken);

        return Results.Created(
            $"/api/channels/{channel.Id}",
            await GetChannelAsync(db, channel.Id, userId, cancellationToken));
    }

    private static async Task<IResult> GetChannelAsync(
        Guid channelId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken) =>
        Results.Ok(await GetChannelAsync(db, channelId, principal.GetUserId(), cancellationToken));

    private static async Task<ChannelDetailDto> GetChannelAsync(
        AppDbContext db,
        Guid channelId,
        long userId,
        CancellationToken cancellationToken)
    {
        var channel = await db.Channels
            .AsNoTracking()
            .Where(c => c.Id == channelId && c.Members.Any(m => m.UserId == userId))
            .Select(c => new ChannelDetailDto(
                c.Id,
                c.Name,
                c.Kind,
                c.OwnerId,
                c.CreatedAtUtc,
                c.Members
                    .OrderBy(m => m.User!.Username)
                    .Select(m => new UserDto(
                        m.UserId,
                        m.User!.Username,
                        m.User.DisplayName,
                        m.User.CreatedAtUtc))
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return channel
            ?? throw ApiException.NotFound("CHANNEL_NOT_FOUND", "Channel not found or you are not a member.");
    }

    private static async Task<IResult> AddMembersAsync(
        Guid channelId,
        AddMembersRequest request,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var channel = await GetChannelForMemberAsync(db, channelId, userId, cancellationToken);
        if (channel.Kind == ChannelKind.Direct)
        {
            throw ApiException.Forbidden("DIRECT_CHANNEL", "Direct channels cannot have more members.");
        }

        var requestedIds = request.UserIds?.Distinct().ToList() ?? [];
        if (requestedIds.Count > MaxMembersPerChannel)
        {
            throw ApiException.BadRequest(
                "TOO_MANY_MEMBERS",
                $"A channel cannot have more than {MaxMembersPerChannel} members.");
        }

        var existingUserIds = await db.Users
            .Where(u => requestedIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
        if (existingUserIds.Count != requestedIds.Count)
        {
            throw ApiException.NotFound("USER_NOT_FOUND", "One or more member ids do not exist.");
        }

        var currentMemberIds = await db.ChannelMembers
            .Where(m => m.ChannelId == channelId)
            .Select(m => m.UserId)
            .ToListAsync(cancellationToken);
        var newMemberIds = existingUserIds
            .Except(currentMemberIds)
            .Where(id => id != userId)
            .ToList();

        if (newMemberIds.Count > 0)
        {
            db.ChannelMembers.AddRange(newMemberIds.Select(memberId =>
                new ChannelMember { ChannelId = channelId, UserId = memberId }));
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(await GetChannelAsync(db, channelId, userId, cancellationToken));
    }

    private static async Task<IResult> JoinChannelAsync(
        Guid channelId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Id == channelId, cancellationToken)
            ?? throw ApiException.NotFound("CHANNEL_NOT_FOUND", "Channel does not exist.");

        if (channel.Kind == ChannelKind.Direct)
        {
            throw ApiException.Forbidden("DIRECT_CHANNEL", "You cannot join a direct channel.");
        }

        var isMember = await db.ChannelMembers.AnyAsync(
            m => m.ChannelId == channelId && m.UserId == userId,
            cancellationToken);
        if (!isMember)
        {
            db.ChannelMembers.Add(new ChannelMember
            {
                ChannelId = channelId,
                UserId = userId
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        return Results.Ok(await GetChannelAsync(db, channelId, userId, cancellationToken));
    }

    private static async Task<IResult> LeaveChannelAsync(
        Guid channelId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var membership = await db.ChannelMembers
            .FirstOrDefaultAsync(
                m => m.ChannelId == channelId && m.UserId == userId,
                cancellationToken)
            ?? throw ApiException.NotFound("CHANNEL_NOT_FOUND", "Channel not found or you are not a member.");

        db.ChannelMembers.Remove(membership);
        await db.SaveChangesAsync(cancellationToken);

        var hasRemainingMembers = await db.ChannelMembers.AnyAsync(
            m => m.ChannelId == channelId,
            cancellationToken);
        if (!hasRemainingMembers)
        {
            var channel = await db.Channels
                .FirstOrDefaultAsync(c => c.Id == channelId, cancellationToken);
            if (channel is not null)
            {
                db.Channels.Remove(channel);
                await db.SaveChangesAsync(cancellationToken);
            }
        }

        return Results.NoContent();
    }

    private static async Task<IResult> GetMessagesAsync(
        Guid channelId,
        long? before,
        int? limit,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        await GetChannelForMemberAsync(db, channelId, userId, cancellationToken);

        var pageSize = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);
        var query = db.Messages
            .AsNoTracking()
            .Where(m => m.ChannelId == channelId);
        if (before.HasValue)
        {
            query = query.Where(m => m.Id < before.Value);
        }

        var page = await query
            .Include(m => m.Sender)
            .OrderByDescending(m => m.Id)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return Results.Ok(new MessagePageDto(
            page.Select(m => m.ToDto()).ToList(),
            page.Count == pageSize ? page[^1].Id : null));
    }

    private static async Task<IResult> SendMessageAsync(
        Guid channelId,
        SendMessageRequest request,
        ClaimsPrincipal principal,
        MessageService messages,
        CancellationToken cancellationToken)
    {
        var result = await messages.SendAsync(
            principal.GetUserId(),
            channelId,
            request.Content,
            request.ClientId,
            cancellationToken);

        return result.Duplicate
            ? Results.Ok(result)
            : Results.Created(
                $"/api/channels/{channelId}/messages/{result.Message.Id}",
                result);
    }

    private static async Task<IResult> MarkReadAsync(
        Guid channelId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var membership = await db.ChannelMembers
            .FirstOrDefaultAsync(
                m => m.ChannelId == channelId && m.UserId == userId,
                cancellationToken)
            ?? throw ApiException.NotFound("CHANNEL_NOT_FOUND", "Channel not found or you are not a member.");

        var readAt = DateTime.UtcNow;
        membership.LastReadAtUtc = readAt;
        await db.SaveChangesAsync(cancellationToken);

        return Results.Ok(new { channelId, readAtUtc = readAt });
    }

    private static async Task<IResult> DeleteChannelAsync(
        Guid channelId,
        ClaimsPrincipal principal,
        AppDbContext db,
        CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        var channel = await db.Channels
            .FirstOrDefaultAsync(c => c.Id == channelId, cancellationToken)
            ?? throw ApiException.NotFound("CHANNEL_NOT_FOUND", "Channel does not exist.");

        if (channel.OwnerId != userId)
        {
            throw ApiException.Forbidden("NOT_OWNER", "Only the channel owner can delete it.");
        }

        db.Channels.Remove(channel);
        await db.SaveChangesAsync(cancellationToken);
        return Results.NoContent();
    }

    private static async Task<Channel> GetChannelForMemberAsync(
        AppDbContext db,
        Guid channelId,
        long userId,
        CancellationToken cancellationToken)
    {
        var channel = await db.Channels
            .AsNoTracking()
            .Where(c => c.Id == channelId && c.Members.Any(m => m.UserId == userId))
            .FirstOrDefaultAsync(cancellationToken);

        return channel
            ?? throw ApiException.NotFound("CHANNEL_NOT_FOUND", "Channel not found or you are not a member.");
    }
}
