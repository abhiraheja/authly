using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class OrganizationMembershipRepository : IOrganizationMembershipRepository
{
    private readonly AppDbContext _db;

    public OrganizationMembershipRepository(AppDbContext db) => _db = db;

    public Task<OrganizationMembership?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.OrganizationMemberships.FirstOrDefaultAsync(m => m.Id == id, ct);

    public Task<OrganizationMembership?> GetAsync(Guid accountId, Guid organizationId, CancellationToken ct = default)
        => _db.OrganizationMemberships.FirstOrDefaultAsync(m => m.AccountId == accountId && m.OrganizationId == organizationId, ct);

    public async Task<IReadOnlyList<OrganizationMembership>> ListByAccountAsync(Guid accountId, CancellationToken ct = default)
        => await _db.OrganizationMemberships.Where(m => m.AccountId == accountId).ToListAsync(ct);

    public async Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => await _db.OrganizationMemberships.Where(m => m.OrganizationId == organizationId).ToListAsync(ct);

    public async Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationWithAccountsAsync(Guid organizationId, CancellationToken ct = default)
        => await _db.OrganizationMemberships
            .Where(m => m.OrganizationId == organizationId)
            .Include(m => m.Account)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(OrganizationMembership membership, CancellationToken ct = default)
    {
        _db.OrganizationMemberships.Add(membership);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(OrganizationMembership membership, CancellationToken ct = default)
    {
        _db.OrganizationMemberships.Update(membership);
        await _db.SaveChangesAsync(ct);
    }
}
