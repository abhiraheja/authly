using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Applications;

/// <summary>
/// Manages a tenant's OAuth clients: creates the protocol registration (OpenIddict) plus the
/// tenant-facing mirror row, generates/rotates secrets (shown once, stored hashed), and removes
/// clients. All operations are tenant-scoped.
/// </summary>
public interface IApplicationService
{
    Task<ApplicationSecretResult> CreateAsync(Guid tenantId, CreateApplicationRequest request, AuditContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<Application>> ListAsync(Guid tenantId, CancellationToken ct = default);

    Task<Application?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    Task<IReadOnlyList<ApplicationSecret>> ListSecretsAsync(Guid tenantId, Guid id, CancellationToken ct = default);

    /// <summary>Issues a new secret for a confidential client and revokes prior ones. Returns the raw secret once.</summary>
    Task<string> RotateSecretAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);
}
