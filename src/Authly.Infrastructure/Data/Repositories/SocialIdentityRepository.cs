using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class SocialIdentityRepository : ISocialIdentityRepository
{
    private readonly AppDbContext _db;

    public SocialIdentityRepository(AppDbContext db) => _db = db;

    public Task<SocialIdentity?> GetAsync(Guid tenantId, string provider, string providerId, CancellationToken ct = default)
        => _db.SocialIdentities.FirstOrDefaultAsync(
            s => s.TenantId == tenantId && s.Provider == provider && s.ProviderId == providerId, ct);

    public async Task<IReadOnlyList<SocialIdentity>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.SocialIdentities
            .Where(s => s.TenantId == tenantId && s.UserId == userId)
            .OrderBy(s => s.Provider)
            .ToListAsync(ct);

    public async Task AddAsync(SocialIdentity identity, CancellationToken ct = default)
    {
        _db.SocialIdentities.Add(identity);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SocialIdentity identity, CancellationToken ct = default)
    {
        _db.SocialIdentities.Update(identity);
        await _db.SaveChangesAsync(ct);
    }
}
