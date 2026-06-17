using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class AccountRepository : IAccountRepository
{
    private readonly AppDbContext _db;

    public AccountRepository(AppDbContext db) => _db = db;

    public Task<Account?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Accounts.FirstOrDefaultAsync(a => a.Id == id, ct);

    public Task<Account?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _db.Accounts.FirstOrDefaultAsync(a => a.Email == email, ct);

    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default)
        => _db.Accounts.AnyAsync(a => a.Email == email, ct);

    public async Task AddAsync(Account account, CancellationToken ct = default)
    {
        _db.Accounts.Add(account);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Account account, CancellationToken ct = default)
    {
        _db.Accounts.Update(account);
        await _db.SaveChangesAsync(ct);
    }
}
