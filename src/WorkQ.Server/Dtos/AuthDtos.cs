namespace WorkQ.Server.Dtos;

public sealed record RegisterRequest(
    string Username,
    string Password,
    string? DisplayName);

public sealed record LoginRequest(string Username, string Password);

public sealed record AuthResponse(string Token, UserDto User);
