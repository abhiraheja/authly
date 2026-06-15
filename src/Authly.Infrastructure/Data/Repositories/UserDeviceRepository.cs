using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

/// <summary>Tenant-scoped persistence for user devices. Every query filters tenant_id (RLS backstop).</summary>
public sealed class UserDeviceRepository : IUserDeviceRepository
{
    private readonly AppDbContext _db;

    public UserDeviceRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<UserDevice>> ListForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => await _db.UserDevices.AsNoTracking()
            .Where(d => d.TenantId == tenantId && d.UserId == userId)
            .OrderByDescending(d => d.LastSeenAt)
            .ToListAsync(ct);

    public Task<UserDevice?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.UserDevices.FirstOrDefaultAsync(d => d.TenantId == tenantId && d.Id == id, ct);

    public Task<UserDevice?> GetByFingerprintAsync(Guid tenantId, Guid userId, string fingerprint, CancellationToken ct = default)
        => _db.UserDevices.FirstOrDefaultAsync(
            d => d.TenantId == tenantId && d.UserId == userId && d.Fingerprint == fingerprint, ct);

    public async Task AddAsync(UserDevice device, CancellationToken ct = default)
    {
        _db.UserDevices.Add(device);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserDevice device, CancellationToken ct = default)
    {
        _db.UserDevices.Update(device);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(UserDevice device, CancellationToken ct = default)
    {
        _db.UserDevices.Remove(device);
        await _db.SaveChangesAsync(ct);
    }
}
