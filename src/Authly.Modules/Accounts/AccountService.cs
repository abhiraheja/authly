using Authly.Core.Enums;
using Authly.Core.Interfaces;
using AccountEntity = Authly.Core.Entities.Account;

namespace Authly.Modules.Accounts;

/// <summary>
/// Console account (operator/employee) credential + lifecycle operations. Operates on the global
/// Account layer (console login), never on tenant end-users (User).
/// </summary>
public interface IAccountService
{
    Task<AccountEntity?> GetAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the account when the email maps to one with a matching password; otherwise null.
    /// An account with no password (invite pending) or that is disabled can never authenticate.</summary>
    Task<AccountEntity?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default);

    Task RecordLoginAsync(Guid id, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AccountService : IAccountService
{
    private readonly IAccountRepository _repo;
    private readonly IPasswordHasher _hasher;

    public AccountService(IAccountRepository repo, IPasswordHasher hasher)
    {
        _repo = repo;
        _hasher = hasher;
    }

    public Task<AccountEntity?> GetAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);

    public async Task<AccountEntity?> ValidateCredentialsAsync(string email, string password, CancellationToken ct = default)
    {
        var account = await _repo.GetByEmailAsync(email.Trim().ToLowerInvariant(), ct);
        if (account is null || account.Status != AccountStatus.Active) return null;
        if (string.IsNullOrEmpty(account.PasswordHash)) return null; // invite pending — cannot sign in
        return _hasher.Verify(account.PasswordHash, password) ? account : null;
    }

    public async Task RecordLoginAsync(Guid id, CancellationToken ct = default)
    {
        var account = await _repo.GetByIdAsync(id, ct);
        if (account is null) return;
        account.LastLoginAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(account, ct);
    }
}
