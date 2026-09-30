namespace WorkQ.Server.Dtos;

public sealed record UserDto(
    long Id,
    string Username,
    string DisplayName,
    DateTime CreatedAtUtc);

public sealed record UserSearchItemDto(
    long Id,
    string Username,
    string DisplayName,
    bool IsOnline);
