namespace Authly.Core.Authorization;

/// <summary>
/// The console-operator RBAC catalogue: system operator-role names, the operator permission set
/// (<c>resource.action</c>), and the default role→permission mapping. Seeded per organization.
/// Entirely separate from the end-user catalogue in <see cref="SystemRbac"/> — operators govern the
/// console; end-user <c>User</c>s get tokens. Permission checks reuse <see cref="PermissionEvaluator"/>.
/// </summary>
public static class OperatorRbac
{
    // --- System operator-role names (unique per org, IsSystem = true) ---
    public const string OrgOwner = "org_owner";
    public const string OrgAdmin = "org_admin";
    public const string ProjectAdmin = "project_admin";
    public const string Viewer = "viewer";

    public static readonly IReadOnlyList<string> RoleNames =
        new[] { OrgOwner, OrgAdmin, ProjectAdmin, Viewer };

    // --- Resources & their actions ---
    public const string ResProject = "project";
    public const string ResClient = "client";
    public const string ResEndUser = "enduser";
    public const string ResMember = "member";
    public const string ResRole = "role";
    public const string ResObservability = "observability";
    public const string ResOrg = "org";
    public const string ResBilling = "billing";

    /// <summary>The operator permission catalogue as <c>resource.action</c> definitions.</summary>
    public static readonly IReadOnlyList<PermissionDefinition> Permissions = BuildPermissions();

    /// <summary>Default permissions (<c>resource.action</c>) granted to each system operator role.</summary>
    public static IReadOnlyList<string> PermissionsFor(string roleName) => roleName switch
    {
        // Everything, including org + billing management. Protected (cannot be removed from the last owner).
        OrgOwner => All(),
        // Everything except org rename/delete and billing.
        OrgAdmin => All().Where(p => p is not ("org.manage" or "billing.manage")).ToArray(),
        // Day-to-day project operation: projects, OAuth clients, end-users, and operator roles.
        ProjectAdmin => new[]
        {
            "project.read", "project.write", "project.create", "project.delete",
            "client.read", "client.manage",
            "enduser.read", "enduser.manage",
            "role.read", "role.manage",
        },
        // Read-only across the console.
        Viewer => Permissions.Where(p => p.Action == "read").Select(p => p.Name).ToArray(),
        _ => Array.Empty<string>()
    };

    public sealed record PermissionDefinition(string Resource, string Action)
    {
        public string Name => $"{Resource}.{Action}";
    }

    private static string[] All() => Permissions.Select(p => p.Name).ToArray();

    private static IReadOnlyList<PermissionDefinition> BuildPermissions()
    {
        PermissionDefinition[] Defs(string resource, params string[] actions)
            => actions.Select(a => new PermissionDefinition(resource, a)).ToArray();

        return new List<PermissionDefinition>()
            .Concat(Defs(ResProject, "read", "write", "create", "delete"))
            .Concat(Defs(ResClient, "read", "manage"))
            .Concat(Defs(ResEndUser, "read", "manage"))
            .Concat(Defs(ResMember, "read", "invite", "manage"))
            .Concat(Defs(ResRole, "read", "manage"))
            .Concat(Defs(ResObservability, "read", "manage"))
            .Concat(Defs(ResOrg, "read", "manage"))
            .Concat(Defs(ResBilling, "read", "manage"))
            .ToList();
    }
}
