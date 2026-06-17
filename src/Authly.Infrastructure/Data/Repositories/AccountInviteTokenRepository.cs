using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class AccountInviteTokenRepository : IAccountInviteTokenRepository
{
    private readonly AppDbContext _db;

    public AccountInviteTokenRepository(AppDbContext db) => _db = db;

    public Task<AccountInviteToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.AccountInviteTokens.Include(t => t.Account).FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(AccountInviteToken token, CancellationToken ct = default)
    {
        _db.AccountInviteTokens.Add(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(AccountInviteToken token, CancellationToken ct = default)
    {
        _db.AccountInviteTokens.Update(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task InvalidateOutstandingAsync(Guid accountId, Guid organizationId, CancellationToken ct = default)
        => await _db.AccountInviteTokens
            .Where(t => t.AccountId == accountId && t.OrganizationId == organizationId && !t.Used)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Used, true), ct);
}
