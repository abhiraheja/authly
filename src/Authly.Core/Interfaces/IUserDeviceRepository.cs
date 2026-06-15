using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Tenant-scoped persistence for a user's known devices.</summary>
public interface IUserDeviceRepository
{
    Task<IReadOnlyList<UserDevice>> ListForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task<UserDevice?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<UserDevice?> GetByFingerprintAsync(Guid tenantId, Guid userId, string fingerprint, CancellationToken ct = default);
    Task AddAsync(UserDevice device, CancellationToken ct = default);
    Task UpdateAsync(UserDevice device, CancellationToken ct = default);
    Task DeleteAsync(UserDevice device, CancellationToken ct = default);
}
