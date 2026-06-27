using Authly.Core.Interfaces;
using Authly.Core.Policies;

namespace Authly.Modules.Policies;

/// <summary>Result of an audience-size preview for a targeting configuration.</summary>
public sealed record AudiencePreview(int? Count, IReadOnlyList<string> SampleEmails, string? Note);

/// <summary>
/// Estimates how many users a policy/survey targeting would currently reach. Precise for the static
/// dimensions (all / roles / providers), which are user properties; application and sign-in-method
/// targeting are evaluated per sign-in (not a static population) so they're reported as a note.
/// </summary>
public interface IAudiencePreviewService
{
    Task<AudiencePreview> PreviewAsync(Guid tenantId, PolicyTargeting targeting, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AudiencePreviewService : IAudiencePreviewService
{
    private readonly IUserRepository _users;
    private readonly IRoleRepository _roles;
    private readonly IUserRoleRepository _userRoles;
    private readonly ISocialIdentityRepository _social;

    public AudiencePreviewService(IUserRepository users, IRoleRepository roles,
        IUserRoleRepository userRoles, ISocialIdentityRepository social)
    {
        _users = users;
        _roles = roles;
        _userRoles = userRoles;
        _social = social;
    }

    public async Task<AudiencePreview> PreviewAsync(Guid tenantId, PolicyTargeting t, CancellationToken ct = default)
    {
        var users = await _users.ListByTenantAsync(tenantId, ct);
        var emailById = users.ToDictionary(u => u.Id, u => u.Email);
        var allIds = users.Select(u => u.Id).ToHashSet();

        // Simple single-dimension audiences.
        switch (t.Audience)
        {
            case Audiences.All:
                return Build(allIds, emailById, null);
            case Audiences.Roles:
                return Build(await RoleUsersAsync(tenantId, t.Roles, ct), emailById, null);
            case Audiences.Providers:
                return Build(await ProviderUsersAsync(tenantId, t.Providers, ct), emailById, null);
            case Audiences.Applications:
                return new AudiencePreview(null, Array.Empty<string>(), "Application targeting is evaluated when a user signs into that app.");
            case Audiences.AuthMethods:
                return new AudiencePreview(null, Array.Empty<string>(), "Sign-in-method targeting is evaluated at sign-in.");
        }

        // Advanced: combine the static dimensions (roles, providers); app/auth-method only refine at sign-in.
        var staticSets = new List<HashSet<Guid>>();
        if (t.Roles.Count > 0) staticSets.Add(await RoleUsersAsync(tenantId, t.Roles, ct));
        if (t.Providers.Count > 0) staticSets.Add(await ProviderUsersAsync(tenantId, t.Providers, ct));
        var dynamicNote = (t.ApplicationIds.Count > 0 || t.AuthMethods.Count > 0)
            ? " Application / sign-in-method conditions are further applied at sign-in."
            : "";

        if (staticSets.Count == 0)
            return new AudiencePreview(null, Array.Empty<string>(),
                ("This targeting is evaluated at sign-in." + dynamicNote).Trim());

        var all = string.Equals(t.Match, "all", StringComparison.OrdinalIgnoreCase);
        var combined = staticSets.Aggregate((acc, s) =>
        {
            acc = new HashSet<Guid>(acc);
            if (all) acc.IntersectWith(s); else acc.UnionWith(s);
            return acc;
        });
        return Build(combined, emailById, string.IsNullOrEmpty(dynamicNote) ? null : dynamicNote.Trim());
    }

    private async Task<HashSet<Guid>> RoleUsersAsync(Guid tenantId, IEnumerable<string> roleNames, CancellationToken ct)
    {
        var set = new HashSet<Guid>();
        foreach (var name in roleNames)
        {
            var role = await _roles.GetRoleByNameAsync(tenantId, name, ct);
            if (role is null) continue;
            foreach (var id in await _userRoles.ListUserIdsForRoleAsync(tenantId, role.Id, ct)) set.Add(id);
        }
        return set;
    }

    private async Task<HashSet<Guid>> ProviderUsersAsync(Guid tenantId, IEnumerable<string> providers, CancellationToken ct)
    {
        var wanted = providers.Select(p => p.ToLowerInvariant()).ToHashSet();
        var byUser = await _social.ListProvidersByTenantAsync(tenantId, ct);
        return byUser.Where(kv => kv.Value.Any(p => wanted.Contains(p.ToLowerInvariant())))
            .Select(kv => kv.Key).ToHashSet();
    }

    private static AudiencePreview Build(HashSet<Guid> ids, IReadOnlyDictionary<Guid, string> emailById, string? note)
    {
        var emails = ids.Where(emailById.ContainsKey).Select(id => emailById[id]).Take(10).ToList();
        return new AudiencePreview(ids.Count, emails, note);
    }
}
