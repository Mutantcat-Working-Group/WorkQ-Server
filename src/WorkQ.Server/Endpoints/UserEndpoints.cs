using Microsoft.EntityFrameworkCore;
using WorkQ.Server.Data;
using WorkQ.Server.Dtos;
using WorkQ.Server.Extensions;
using WorkQ.Server.Services;

namespace WorkQ.Server.Endpoints;

public static class UserEndpoints
{
    public static RouteGroupBuilder MapUserEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/me", GetCurrentUserAsync);
        group.MapGet("", SearchUsersAsync);
        return group;
    }

    private static async Task<IResult> GetCurrentUserAsync(
        AppDbContext db,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.User.GetUserId();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw ApiException.NotFound("USER_NOT_FOUND", "User no longer exists.");
        }

        return Results.Ok(user.ToDto());
    }

    private static async Task<IResult> SearchUsersAsync(
        string? query,
        CancellationToken cancellationToken,
        AppDbContext db,
        PresenceService presence)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return Results.Ok(Array.Empty<UserSearchItemDto>());
        }

        var normalized = query.Trim().ToLowerInvariant();
        var users = await db.Users
            .Where(u => u.Username.Contains(normalized)
                || u.DisplayName.ToLower().Contains(normalized))
            .OrderBy(u => u.Username)
            .Take(50)
            .Select(u => new UserSearchItemDto(
                u.Id,
                u.Username,
                u.DisplayName,
                false))
            .ToListAsync(cancellationToken);

        var result = users.Select(u => u with
        {
            IsOnline = presence.IsOnline(u.Id)
        }).ToList();

        return Results.Ok(result);
    }
}
