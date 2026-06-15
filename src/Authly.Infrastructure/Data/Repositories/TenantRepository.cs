using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class TenantRepository : ITenantRepository
{
    private readonly AppDbContext _db;

    public TenantRepository(AppDbContext db) => _db = db;

    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Tenants.FirstOrDefaultAsync(t => t.Id == id, ct);

    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
        => _db.Tenants.FirstOrDefaultAsync(t => t.Slug == slug, ct);

    public Task<Tenant?> GetByCustomDomainOrNullAsync(string host, CancellationToken ct = default)
        => _db.Tenants.FirstOrDefaultAsync(t => t.CustomDomain != null && t.CustomDomain == host, ct);

    public async Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default)
        => await _db.Tenants
            .Where(t => t.Status != TenantStatus.Deleted)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(ct);

    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
        => _db.Tenants.AnyAsync(t => t.Slug == slug, ct);

    public async Task AddAsync(Tenant tenant, CancellationToken ct = default)
    {
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Tenant tenant, CancellationToken ct = default)
    {
        _db.Tenants.Update(tenant);
        await _db.SaveChangesAsync(ct);
    }
}
