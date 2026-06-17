using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for the global console <see cref="Account"/>. Implemented in Infrastructure.</summary>
public interface IAccountRepository
{
    Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Account?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken ct = default);
    Task AddAsync(Account account, CancellationToken ct = default);
    Task UpdateAsync(Account account, CancellationToken ct = default);
}
