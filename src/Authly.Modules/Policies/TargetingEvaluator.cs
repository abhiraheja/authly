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
    public static bool Matches(PolicyTargeting t, Guid? appId, string? authCategory,
        ISet<string>? linkedProviders, ISet<string>? userRoles = null) => t.Audience switch
    {
        Audiences.All => true,
        Audiences.Applications => AppMatch(t, appId),
        Audiences.AuthMethods => AuthMatch(t, authCategory),
        Audiences.Providers => ProviderMatch(t, linkedProviders),
        Audiences.Roles => RoleMatch(t, userRoles),
        Audiences.Advanced => AdvancedMatch(t, appId, authCategory, linkedProviders, userRoles),
        _ => false
    };

    private static bool AppMatch(PolicyTargeting t, Guid? appId) => appId is { } id && t.ApplicationIds.Contains(id);
    private static bool AuthMatch(PolicyTargeting t, string? authCategory) => authCategory is { Length: > 0 } && t.AuthMethods.Contains(authCategory);
    private static bool ProviderMatch(PolicyTargeting t, ISet<string>? providers) => providers is not null && t.Providers.Any(p => providers.Contains(p.ToLowerInvariant()));
    private static bool RoleMatch(PolicyTargeting t, ISet<string>? roles) => roles is not null && t.Roles.Any(r => roles.Contains(r));

    /// <summary>Combine every populated dimension with the configured any/all match. No dimensions → matches all.</summary>
    private static bool AdvancedMatch(PolicyTargeting t, Guid? appId, string? authCategory, ISet<string>? providers, ISet<string>? roles)
    {
        var results = new List<bool>();
        if (t.ApplicationIds.Count > 0) results.Add(AppMatch(t, appId));
        if (t.AuthMethods.Count > 0) results.Add(AuthMatch(t, authCategory));
        if (t.Providers.Count > 0) results.Add(ProviderMatch(t, providers));
        if (t.Roles.Count > 0) results.Add(RoleMatch(t, roles));
        if (results.Count == 0) return true;
        return string.Equals(t.Match, "all", StringComparison.OrdinalIgnoreCase) ? results.All(x => x) : results.Any(x => x);
    }

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

    /// <summary>The user's end-user role names.</summary>
    public static async Task<HashSet<string>> UserRolesAsync(IUserRoleRepository userRoles, Guid tenantId, Guid userId, CancellationToken ct)
        => (await userRoles.GetRoleNamesAsync(tenantId, userId, ct)).ToHashSet();
}
