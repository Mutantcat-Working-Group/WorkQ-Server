using WorkQ.Server.Models;

namespace WorkQ.Server.Dtos;

public sealed record CreateChannelRequest(
    ChannelKind Kind,
    string? Name,
    IReadOnlyList<long>? MemberIds);

public sealed record AddMembersRequest(IReadOnlyList<long> UserIds);

public sealed record ChannelSummaryDto(
    Guid Id,
    string Name,
    ChannelKind Kind,
    long OwnerId,
    int MemberCount,
    DateTime CreatedAtUtc,
    MessageDto? LastMessage,
    int UnreadCount,
    DateTime? LastReadAtUtc);

public sealed record ChannelDetailDto(
    Guid Id,
    string Name,
    ChannelKind Kind,
    long OwnerId,
    DateTime CreatedAtUtc,
    IReadOnlyList<UserDto> Members);
