using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace WorkQ.Server.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (!long.TryParse(value, out var userId))
        {
            throw new InvalidOperationException("User id claim is missing.");
        }

        return userId;
    }

    public static string GetUsername(this ClaimsPrincipal principal) =>
        principal.FindFirstValue(JwtRegisteredClaimNames.Name)
        ?? principal.FindFirstValue(ClaimTypes.Name)
        ?? string.Empty;
}
