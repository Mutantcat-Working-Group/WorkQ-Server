using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using WorkQ.Server.Data;
using WorkQ.Server.Dtos;
using WorkQ.Server.Models;
using WorkQ.Server.Services;

namespace WorkQ.Server.Endpoints;

public static partial class AuthEndpoints
{
    private static readonly Regex UsernamePattern = CreateUsernamePattern();

    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder routes)
    {
        var group = routes.MapGroup("/api/auth").WithTags("Auth");
        group.MapPost("/register", RegisterAsync);
        group.MapPost("/login", LoginAsync);
        return group;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        AppDbContext db,
        JwtTokenService tokens,
        CancellationToken cancellationToken)
    {
        var errors = ValidateRegister(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var username = NormalizeUsername(request.Username);
        if (await db.Users.AnyAsync(u => u.Username == username, cancellationToken))
        {
            throw ApiException.Conflict("USERNAME_TAKEN", "This username is already taken.");
        }

        var (hash, salt) = PasswordHasher.Create(request.Password);
        var user = new User
        {
            Username = username,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName)
                ? username
                : request.DisplayName.Trim(),
            PasswordHash = hash,
            PasswordSalt = salt
        };

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw ApiException.Conflict("USERNAME_TAKEN", "This username is already taken.");
        }

        var token = tokens.CreateToken(user.Id, user.Username);
        return Results.Created($"/api/users/{user.Id}", new AuthResponse(token, user.ToDto()));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        AppDbContext db,
        JwtTokenService tokens,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["Credentials"] = ["Username and password are required."]
            });
        }

        var username = NormalizeUsername(request.Username);
        var user = await db.Users.SingleOrDefaultAsync(
            u => u.Username == username,
            cancellationToken);

        if (user is null || !PasswordHasher.Verify(request.Password, user.PasswordHash, user.PasswordSalt))
        {
            throw ApiException.Unauthorized("INVALID_CREDENTIALS", "Username or password is incorrect.");
        }

        var token = tokens.CreateToken(user.Id, user.Username);
        return Results.Ok(new AuthResponse(token, user.ToDto()));
    }

    private static Dictionary<string, string[]> ValidateRegister(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        var username = request.Username?.Trim() ?? string.Empty;

        if (!UsernamePattern.IsMatch(username))
        {
            errors["Username"] =
                ["Username must be 3-32 characters and may contain letters, digits, _, . and -."];
        }

        if (string.IsNullOrEmpty(request.Password) || request.Password.Length is < 8 or > 128)
        {
            errors["Password"] = ["Password must be between 8 and 128 characters."];
        }

        if (!string.IsNullOrWhiteSpace(request.DisplayName) && request.DisplayName.Trim().Length > 64)
        {
            errors["DisplayName"] = ["Display name cannot exceed 64 characters."];
        }

        return errors;
    }

    private static string NormalizeUsername(string username) =>
        username.Trim().ToLowerInvariant();

    [GeneratedRegex(@"^[a-zA-Z0-9_.-]{3,32}$", RegexOptions.Compiled)]
    private static partial Regex CreateUsernamePattern();
}
