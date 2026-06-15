using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class OtpCodeRepository : IOtpCodeRepository
{
    private readonly AppDbContext _db;

    public OtpCodeRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(OtpCode code, CancellationToken ct = default)
    {
        _db.OtpCodes.Add(code);
        await _db.SaveChangesAsync(ct);
    }

    public Task<OtpCode?> GetLatestActiveAsync(Guid tenantId, Guid userId, OtpChannel channel, CancellationToken ct = default)
        => _db.OtpCodes
            .Where(o => o.TenantId == tenantId && o.UserId == userId && o.Channel == channel
                        && !o.Used && o.ExpiresAt > DateTimeOffset.UtcNow)
            .OrderByDescending(o => o.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task InvalidateOutstandingAsync(Guid tenantId, Guid userId, OtpChannel channel, CancellationToken ct = default)
        => _db.OtpCodes
            .Where(o => o.TenantId == tenantId && o.UserId == userId && o.Channel == channel && !o.Used)
            .ExecuteUpdateAsync(o => o.SetProperty(x => x.Used, true), ct);

    public async Task UpdateAsync(OtpCode code, CancellationToken ct = default)
    {
        _db.OtpCodes.Update(code);
        await _db.SaveChangesAsync(ct);
    }
}
