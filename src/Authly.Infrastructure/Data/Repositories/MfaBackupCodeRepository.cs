using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class MfaBackupCodeRepository : IMfaBackupCodeRepository
{
    private readonly AppDbContext _db;

    public MfaBackupCodeRepository(AppDbContext db) => _db = db;

    public async Task AddRangeAsync(IEnumerable<MfaBackupCode> codes, CancellationToken ct = default)
    {
        _db.MfaBackupCodes.AddRange(codes);
        await _db.SaveChangesAsync(ct);
    }

    public Task<MfaBackupCode?> GetUnusedByHashAsync(Guid userId, string codeHash, CancellationToken ct = default)
        => _db.MfaBackupCodes.FirstOrDefaultAsync(
            c => c.UserId == userId && c.CodeHash == codeHash && !c.Used, ct);

    public Task<int> CountUnusedAsync(Guid userId, CancellationToken ct = default)
        => _db.MfaBackupCodes.CountAsync(c => c.UserId == userId && !c.Used, ct);

    public Task DeleteAllForUserAsync(Guid userId, CancellationToken ct = default)
        => _db.MfaBackupCodes.Where(c => c.UserId == userId).ExecuteDeleteAsync(ct);

    public async Task UpdateAsync(MfaBackupCode code, CancellationToken ct = default)
    {
        _db.MfaBackupCodes.Update(code);
        await _db.SaveChangesAsync(ct);
    }
}
