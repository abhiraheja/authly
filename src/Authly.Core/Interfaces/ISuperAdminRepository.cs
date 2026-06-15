using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for <see cref="SuperAdmin"/>. Implemented in Infrastructure.</summary>
public interface ISuperAdminRepository
{
    Task<SuperAdmin?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SuperAdmin?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> AnyAsync(CancellationToken ct = default);
    Task AddAsync(SuperAdmin admin, CancellationToken ct = default);
    Task UpdateAsync(SuperAdmin admin, CancellationToken ct = default);
}
