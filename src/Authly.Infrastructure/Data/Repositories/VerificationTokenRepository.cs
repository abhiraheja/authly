using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class VerificationTokenRepository : IVerificationTokenRepository
{
    private readonly AppDbContext _db;

    public VerificationTokenRepository(AppDbContext db) => _db = db;

    public Task<VerificationToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => _db.VerificationTokens.FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    public async Task AddAsync(VerificationToken token, CancellationToken ct = default)
    {
        _db.VerificationTokens.Add(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(VerificationToken token, CancellationToken ct = default)
    {
        _db.VerificationTokens.Update(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task InvalidateOutstandingAsync(Guid userId, string type, CancellationToken ct = default)
        => await _db.VerificationTokens
            .Where(t => t.UserId == userId && t.Type == type && !t.Used)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Used, true), ct);
}
