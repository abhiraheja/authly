using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for <see cref="Organization"/> (global / RLS-exempt). Implemented in Infrastructure.</summary>
public interface IOrganizationRepository
{
    Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Organization?> GetBySlugAsync(string slug, CancellationToken ct = default);
    Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default);
    Task AddAsync(Organization organization, CancellationToken ct = default);
    Task UpdateAsync(Organization organization, CancellationToken ct = default);
}
