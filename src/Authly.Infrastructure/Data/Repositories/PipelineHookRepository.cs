using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class PipelineHookRepository : IPipelineHookRepository
{
    private readonly AppDbContext _db;

    public PipelineHookRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<PipelineHook>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.PipelineHooks
            .Where(h => h.TenantId == tenantId)
            .OrderBy(h => h.Stage).ThenBy(h => h.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<PipelineHook>> ListActiveByStageAsync(Guid tenantId, PipelineStage stage, CancellationToken ct = default)
        => await _db.PipelineHooks
            .Where(h => h.TenantId == tenantId && h.Stage == stage && h.IsActive)
            .OrderBy(h => h.CreatedAt)
            .ToListAsync(ct);

    public Task<PipelineHook?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.PipelineHooks.FirstOrDefaultAsync(h => h.TenantId == tenantId && h.Id == id, ct);

    public async Task AddAsync(PipelineHook hook, CancellationToken ct = default)
    {
        _db.PipelineHooks.Add(hook);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PipelineHook hook, CancellationToken ct = default)
    {
        _db.PipelineHooks.Update(hook);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(PipelineHook hook, CancellationToken ct = default)
    {
        _db.PipelineHooks.Remove(hook);
        await _db.SaveChangesAsync(ct);
    }
}
