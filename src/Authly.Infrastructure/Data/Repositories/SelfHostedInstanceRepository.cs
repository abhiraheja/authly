using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

/// <summary>Platform-level (not tenant-scoped) persistence for self-hosted instance registrations.</summary>
public sealed class SelfHostedInstanceRepository : ISelfHostedInstanceRepository
{
    private readonly AppDbContext _db;

    public SelfHostedInstanceRepository(AppDbContext db) => _db = db;

    public Task<SelfHostedInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.SelfHostedInstances.FirstOrDefaultAsync(i => i.Id == id, ct);

    public Task<SelfHostedInstance?> GetBySyncKeyHashAsync(string syncKeyHash, CancellationToken ct = default)
        => _db.SelfHostedInstances.FirstOrDefaultAsync(i => i.SyncKeyHash == syncKeyHash, ct);

    public async Task<IReadOnlyList<SelfHostedInstance>> ListAsync(CancellationToken ct = default)
        => await _db.SelfHostedInstances.OrderByDescending(i => i.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(SelfHostedInstance instance, CancellationToken ct = default)
    {
        _db.SelfHostedInstances.Add(instance);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SelfHostedInstance instance, CancellationToken ct = default)
    {
        _db.SelfHostedInstances.Update(instance);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(SelfHostedInstance instance, CancellationToken ct = default)
    {
        _db.SelfHostedInstances.Remove(instance);
        await _db.SaveChangesAsync(ct);
    }
}
