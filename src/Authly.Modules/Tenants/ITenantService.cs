using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Tenants;

/// <summary>Tenant (organization) lifecycle — used by the Super Admin panel.</summary>
public interface ITenantService
{
    Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default);
    Task<Tenant?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Creates a tenant. Throws <see cref="SlugAlreadyExistsException"/> on slug collision.</summary>
    Task<Tenant> CreateAsync(CreateTenantRequest request, AuditContext actor, CancellationToken ct = default);

    Task SuspendAsync(Guid id, AuditContext actor, CancellationToken ct = default);
    Task ReactivateAsync(Guid id, AuditContext actor, CancellationToken ct = default);

    /// <summary>Soft-deletes (status = Deleted); honors the offboarding grace model.</summary>
    Task DeleteAsync(Guid id, AuditContext actor, CancellationToken ct = default);
}
