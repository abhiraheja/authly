namespace Authly.Core.Authorization;

/// <summary>
/// The platform's built-in RBAC catalogue: system role names, the baseline permission set
/// (<c>resource.action</c>), and the default role→permission mapping. These are seeded per tenant
/// and are the source of truth for <see cref="SeedDefinitions"/>. Custom tenant roles/permissions
/// live alongside these but are never part of this catalogue.
/// </summary>
public static class SystemRbac
{
    // --- System role names (unique per tenant, IsSystem = true) ---
    public const string SuperAdmin = "super_admin";
    public const string TenantAdmin = "tenant_admin";
    public const string TenantMember = "tenant_member";
    public const string TenantViewer = "tenant_viewer";
    public const string ServiceAccount = "service_account";

    public static readonly IReadOnlyList<string> RoleNames =
        new[] { SuperAdmin, TenantAdmin, TenantMember, TenantViewer, ServiceAccount };

    // --- Resources & actions ---
    public const string ActionRead = "read";
    public const string ActionWrite = "write";
    public const string ActionDelete = "delete";

    /// <summary>Resources that participate in the baseline read/write/delete permission grid.</summary>
    public static readonly IReadOnlyList<string> Resources =
        new[] { "user", "role", "application", "audit" };

    /// <summary>The baseline permission set as <c>resource.action</c> definitions.</summary>
    public static readonly IReadOnlyList<PermissionDefinition> Permissions = BuildPermissions();

    /// <summary>Default permissions (<c>resource.action</c>) granted to each system role.</summary>
    public static IReadOnlyList<string> PermissionsFor(string roleName) => roleName switch
    {
        // Full tenant control.
        SuperAdmin or TenantAdmin => All(),
        // Read everything; write/delete the day-to-day resources, never roles or audit.
        TenantMember => new[] { "user.read", "user.write", "role.read", "application.read", "application.write", "audit.read" },
        // Read-only across the tenant.
        TenantViewer => Resources.Select(r => $"{r}.{ActionRead}").ToArray(),
        // Machine identity: read users by default; tenants widen this via custom roles.
        ServiceAccount => new[] { "user.read" },
        _ => Array.Empty<string>()
    };

    public sealed record PermissionDefinition(string Resource, string Action)
    {
        public string Name => $"{Resource}.{Action}";
    }

    private static string[] All() => Permissions.Select(p => p.Name).ToArray();

    private static IReadOnlyList<PermissionDefinition> BuildPermissions()
    {
        var list = new List<PermissionDefinition>();
        foreach (var r in new[] { "user", "role", "application" })
        {
            list.Add(new(r, ActionRead));
            list.Add(new(r, ActionWrite));
            list.Add(new(r, ActionDelete));
        }
        list.Add(new("audit", ActionRead)); // audit logs are append-only: read only
        return list;
    }
}
