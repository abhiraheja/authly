using System.Security.Claims;
using Authly.Core.Authorization;

namespace Authly.Web.Infrastructure.Authorization;

/// <summary>Reads a principal's permission claims and evaluates them against a required permission.</summary>
public static class PermissionChecker
{
    public static bool HasPermission(ClaimsPrincipal user, string required)
    {
        var granted = user.FindAll(TokenClaims.Permissions).Select(c => c.Value);
        return PermissionEvaluator.Satisfies(granted, required);
    }
}
