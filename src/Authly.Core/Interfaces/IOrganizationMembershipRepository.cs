using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for <see cref="OrganizationMembership"/> (global / RLS-exempt). Implemented in Infrastructure.</summary>
public interface IOrganizationMembershipRepository
{
    Task<OrganizationMembership?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<OrganizationMembership?> GetAsync(Guid accountId, Guid organizationId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationMembership>> ListByAccountAsync(Guid accountId, CancellationToken ct = default);
    Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default);
    Task AddAsync(OrganizationMembership membership, CancellationToken ct = default);
    Task UpdateAsync(OrganizationMembership membership, CancellationToken ct = default);
}
