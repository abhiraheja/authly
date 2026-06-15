using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class SocialProviderRepository : ISocialProviderRepository
{
    private readonly AppDbContext _db;

    public SocialProviderRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<SocialProvider>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.SocialProviders
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Provider)
            .ToListAsync(ct);

    public Task<SocialProvider?> GetAsync(Guid tenantId, string provider, CancellationToken ct = default)
        => _db.SocialProviders.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Provider == provider, ct);

    public Task<SocialProvider?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.SocialProviders.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task AddAsync(SocialProvider provider, CancellationToken ct = default)
    {
        _db.SocialProviders.Add(provider);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SocialProvider provider, CancellationToken ct = default)
    {
        _db.SocialProviders.Update(provider);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(SocialProvider provider, CancellationToken ct = default)
    {
        _db.SocialProviders.Remove(provider);
        await _db.SaveChangesAsync(ct);
    }
}
