using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class OrganizationRepository : IOrganizationRepository
{
    private readonly AppDbContext _db;

    public OrganizationRepository(AppDbContext db) => _db = db;

    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Organizations.FirstOrDefaultAsync(o => o.Id == id, ct);

    public Task<Organization?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Organizations.FirstOrDefaultAsync(o => o.Slug == slug, ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => _db.Organizations.AnyAsync(o => o.Slug == slug, ct);

    public async Task AddAsync(Organization organization, CancellationToken ct = default)
    {
        _db.Organizations.Add(organization);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Organization organization, CancellationToken ct = default)
    {
        _db.Organizations.Update(organization);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Organization organization, CancellationToken ct = default)
    {
        _db.Organizations.Remove(organization);
        await _db.SaveChangesAsync(ct);
    }
}
