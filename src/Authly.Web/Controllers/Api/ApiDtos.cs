using System.ComponentModel.DataAnnotations;
using Authly.Core.Entities;

namespace Authly.Web.Controllers.Api;

// --- Response DTOs (never expose entity internals like password hashes) ---

public sealed record UserResponse(
    Guid Id, string Email, bool EmailVerified, string? FirstName, string? LastName,
    string? Phone, string Status, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt)
{
    public static UserResponse From(User u) => new(
        u.Id, u.Email, u.EmailVerified, u.FirstName, u.LastName, u.Phone,
        u.Status.ToString(), u.CreatedAt, u.LastLoginAt);
}

public sealed record RoleResponse(Guid Id, string Name, string? Description, bool IsSystem, DateTimeOffset CreatedAt)
{
    public static RoleResponse From(Role r) => new(r.Id, r.Name, r.Description, r.IsSystem, r.CreatedAt);
}

public sealed record PermissionResponse(Guid Id, string Resource, string Action, string Name, string? Description)
{
    public static PermissionResponse From(Permission p) => new(p.Id, p.Resource, p.Action, p.Name, p.Description);
}

public sealed record ApplicationResponse(
    Guid Id, string ClientId, string Name, string Type, IReadOnlyList<string> GrantTypes,
    IReadOnlyList<string> RedirectUris, IReadOnlyList<string> PostLogoutRedirectUris,
    IReadOnlyList<string> AllowedScopes, bool IsConfidential, DateTimeOffset CreatedAt)
{
    public static ApplicationResponse From(Application a) => new(
        a.Id, a.ClientId, a.Name, a.Type.ToString(), a.GrantTypes, a.RedirectUris, a.PostLogoutRedirectUris,
        a.AllowedScopes, a.IsConfidential, a.CreatedAt);
}

public sealed record SessionResponse(
    Guid Id, string? IpAddress, string? UserAgent, string? DeviceFingerprint, string? Location,
    DateTimeOffset LastActiveAt, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt)
{
    public static SessionResponse From(Session s) => new(
        s.Id, s.IpAddress, s.UserAgent, s.DeviceFingerprint, s.Location, s.LastActiveAt, s.ExpiresAt, s.CreatedAt);
}

public sealed record AuditLogResponse(
    Guid Id, string Event, Guid? ActorId, string? ActorType, string? ResourceType, Guid? ResourceId,
    string Result, string? IpAddress, DateTimeOffset CreatedAt)
{
    public static AuditLogResponse From(AuditLog a) => new(
        a.Id, a.Event, a.ActorId, a.ActorType, a.ResourceType, a.ResourceId, a.Result, a.IpAddress, a.CreatedAt);
}

// --- Request DTOs (validated at the controller boundary) ---

public sealed class CreateUserDto
{
    [Required, EmailAddress] public string Email { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool EmailVerified { get; set; }

    /// <summary>When true, suppress the <c>user.created</c> webhook (bulk import/migration that
    /// provisions downstream state itself). The audit row is still written.</summary>
    public bool SuppressEvents { get; set; }
}

public sealed class UpdateUserDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Phone { get; set; }
    public string? Timezone { get; set; }
    public string? Locale { get; set; }
}

public sealed class AssignRoleDto
{
    [Required] public Guid RoleId { get; set; }
}

public sealed class CreateRoleDto
{
    [Required] public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public sealed class SetRolePermissionsDto
{
    public Guid[] PermissionIds { get; set; } = Array.Empty<Guid>();
}

public sealed class CreateApplicationDto
{
    [Required] public string Name { get; set; } = string.Empty;
    [Required] public Authly.Core.Enums.ApplicationType Type { get; set; }
    public string[] RedirectUris { get; set; } = Array.Empty<string>();
    public string[] PostLogoutRedirectUris { get; set; } = Array.Empty<string>();
    public string[] Scopes { get; set; } = Array.Empty<string>();
}
