using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Slae.WebApi.Extensions;

public static class CurrentUserExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal user)
    {
        string? sub = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        return Guid.TryParse(sub, out Guid id)
            ? id
            : throw new InvalidOperationException("Token does not contain a valid 'sub' claim.");
    }
}