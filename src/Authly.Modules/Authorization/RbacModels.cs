using Authly.Core.Entities;

namespace Authly.Modules.Authorization;

/// <summary>Request to create a custom tenant role.</summary>
public sealed record CreateRoleRequest(string Name, string? Description);

/// <summary>A user's effective authorization: role names + flattened permissions, for token claims.</summary>
public sealed record UserAuthorization(IReadOnlyList<string> Roles, IReadOnlyList<string> Permissions)
{
    public static readonly UserAuthorization None = new(Array.Empty<string>(), Array.Empty<string>());
}

/// <summary>A role together with the permission ids it currently grants (for the edit UI).</summary>
public sealed record RoleWithPermissions(Role Role, IReadOnlyList<Guid> PermissionIds);

public sealed class RoleNotFoundException(Guid id) : Exception($"Role {id} was not found.");

public sealed class RoleNameAlreadyExistsException(string name) : Exception($"A role named '{name}' already exists.");

public sealed class SystemRoleProtectedException(string name)
    : Exception($"The system role '{name}' cannot be modified or deleted.");
