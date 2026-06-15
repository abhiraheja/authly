using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class ApiKeyRepository : IApiKeyRepository
{
    private readonly AppDbContext _db;

    public ApiKeyRepository(AppDbContext db) => _db = db;

    public Task<ApiKey?> GetByHashAsync(string keyHash, CancellationToken ct = default)
        => _db.ApiKeys.FirstOrDefaultAsync(k => k.KeyHash == keyHash, ct);

    public async Task<IReadOnlyList<ApiKey>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.ApiKeys
            .Where(k => k.TenantId == tenantId)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

    public Task<ApiKey?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.ApiKeys.FirstOrDefaultAsync(k => k.TenantId == tenantId && k.Id == id, ct);

    public async Task AddAsync(ApiKey key, CancellationToken ct = default)
    {
        _db.ApiKeys.Add(key);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(ApiKey key, CancellationToken ct = default)
    {
        _db.ApiKeys.Update(key);
        await _db.SaveChangesAsync(ct);
    }

    public Task TouchLastUsedAsync(Guid id, DateTimeOffset whenUtc, CancellationToken ct = default)
        => _db.ApiKeys
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(k => k.SetProperty(x => x.LastUsedAt, whenUtc), ct);
}
