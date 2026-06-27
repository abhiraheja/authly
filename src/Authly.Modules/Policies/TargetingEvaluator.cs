using Authly.Core.Interfaces;
using Authly.Core.Policies;

namespace Authly.Modules.Policies;

/// <summary>
/// Shared targeting evaluation for the policies + surveys engines: matches a <see cref="PolicyTargeting"/>
/// against the current sign-in context (application being signed into, auth-method category, linked
/// social providers). The auth-method/provider signals are resolved lazily from the repositories.
/// </summary>
public static class TargetingEvaluator
{
    public static bool Matches(PolicyTargeting t, Guid? appId, string? authCategory, ISet<string>? linkedProviders) => t.Audience switch
    {
        Audiences.All => true,
        Audiences.Applications => appId is { } id && t.ApplicationIds.Contains(id),
        Audiences.AuthMethods => authCategory is { Length: > 0 } && t.AuthMethods.Contains(authCategory),
        Audiences.Providers => linkedProviders is not null && t.Providers.Any(p => linkedProviders.Contains(p.ToLowerInvariant())),
        _ => false
    };

    /// <summary>The normalized auth-method category of the user's most recent successful login, or null.</summary>
    public static async Task<string?> AuthCategoryAsync(ILoginHistoryRepository logins, Guid tenantId, Guid userId, CancellationToken ct)
    {
        var history = await logins.ListForUserAsync(tenantId, userId, limit: 10, ct);
        var method = history
            .Where(h => string.Equals(h.Result, "success", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => h.Method)
            .FirstOrDefault();
        return AuthMethodCategories.Normalize(method);
    }

    /// <summary>The set of social provider keys linked to the user (lower-cased).</summary>
    public static async Task<HashSet<string>> LinkedProvidersAsync(ISocialIdentityRepository social, Guid tenantId, Guid userId, CancellationToken ct)
        => (await social.ListByUserAsync(tenantId, userId, ct)).Select(s => s.Provider.ToLowerInvariant()).ToHashSet();
}
