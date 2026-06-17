using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for <see cref="Tenant"/>. Implemented in Infrastructure.</summary>
public interface ITenantRepository
{
    Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<Tenant?> GetByCustomDomainOrNullAsync(string host, CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Tenant>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Tenant tenant, CancellationToken ct = default);
    Task UpdateAsync(Tenant tenant, CancellationToken ct = default);
}
