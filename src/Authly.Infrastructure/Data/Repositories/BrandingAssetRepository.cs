using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class BrandingAssetRepository : IBrandingAssetRepository
{
    private readonly AppDbContext _db;

    public BrandingAssetRepository(AppDbContext db) => _db = db;

    public Task<BrandingAsset?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.BrandingAssets.FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task AddAsync(BrandingAsset asset, CancellationToken ct = default)
    {
        _db.BrandingAssets.Add(asset);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteByKindAsync(Guid tenantId, string kind, CancellationToken ct = default)
        => await _db.BrandingAssets
            .Where(a => a.TenantId == tenantId && a.Kind == kind)
            .ExecuteDeleteAsync(ct);
}
