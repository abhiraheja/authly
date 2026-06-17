using Authly.Core.Enums;
using Authly.Core.Interfaces;

namespace Authly.Modules.Operators;

/// <summary>The resolved console authorization for an operator working in a given org+project:
/// their membership id, operator role names, and flattened operator permission set.</summary>
public sealed record ConsoleAccess(Guid MembershipId, IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions);

/// <summary>
/// Resolves whether an account may operate the console for a given (org, project) and, if so, their
/// effective operator permissions. The console-operator counterpart of
/// <c>RbacService.GetUserAuthorizationAsync</c> — additionally enforcing membership + project-in-org.
/// </summary>
public interface IConsoleAccessService
{
    /// <summary>Returns the effective console access when the account has an Active membership in the
    /// org AND the project belongs to that org; otherwise null (caller must deny + sign out).</summary>
    Task<ConsoleAccess?> ResolveAsync(Guid accountId, Guid organizationId, Guid projectId, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ConsoleAccessService : IConsoleAccessService
{
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly ITenantRepository _tenants;
    private readonly IMemberRoleRepository _memberRoles;

    public ConsoleAccessService(
        IOrganizationMembershipRepository memberships,
        ITenantRepository tenants,
        IMemberRoleRepository memberRoles)
    {
        _memberships = memberships;
        _tenants = tenants;
        _memberRoles = memberRoles;
    }

    public async Task<ConsoleAccess?> ResolveAsync(Guid accountId, Guid organizationId, Guid projectId, CancellationToken ct = default)
    {
        // 1) Account must have an Active membership in the org.
        var membership = await _memberships.GetAsync(accountId, organizationId, ct);
        if (membership is null || membership.Status != MembershipStatus.Active) return null;

        // 2) The active project must belong to the active org.
        var project = await _tenants.GetByIdAsync(projectId, ct);
        if (project is null || project.OrganizationId != organizationId) return null;

        // 3) Effective operator roles + permissions for this membership.
        var roles = await _memberRoles.GetRoleNamesAsync(membership.Id, ct);
        var permissions = await _memberRoles.GetPermissionNamesAsync(membership.Id, ct);
        return new ConsoleAccess(membership.Id, roles, permissions);
    }
}
