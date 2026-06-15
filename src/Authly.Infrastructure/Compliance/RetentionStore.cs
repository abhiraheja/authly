using Authly.Core.Compliance;
using Authly.Core.Interfaces;
using Authly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Compliance;

/// <summary>
/// Bulk time-based purges for the retention jobs. Global targets (tokens without a tenant column,
/// audit logs) clear in one statement. RLS-protected targets (OTP codes, login history) require a
/// bound tenant scope — <see cref="BindTenantScopeAsync"/> sets it via the tenant context, which
/// the connection interceptor applies as <c>app.current_tenant</c> on the next query.
/// </summary>
public sealed class RetentionStore : IRetentionStore
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;

    public RetentionStore(AppDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public Task<int> PurgeExpiredVerificationTokensAsync(DateTimeOffset now, CancellationToken ct = default)
        => _db.VerificationTokens.Where(t => t.ExpiresAt < now).ExecuteDeleteAsync(ct);

    public Task<int> PurgeExpiredPasswordResetTokensAsync(DateTimeOffset now, CancellationToken ct = default)
        => _db.PasswordResetTokens.Where(t => t.ExpiresAt < now).ExecuteDeleteAsync(ct);

    public Task<int> PurgeOldAuditLogsAsync(DateTimeOffset cutoff, CancellationToken ct = default)
        => _db.AuditLogs.Where(a => a.CreatedAt < cutoff).ExecuteDeleteAsync(ct);

    public Task<int> PurgeExpiredOtpCodesAsync(DateTimeOffset now, CancellationToken ct = default)
    {
        var tid = _tenant.TenantId;
        // Filter by tenant_id explicitly (hard rule) in addition to the RLS backstop.
        return _db.OtpCodes.Where(o => o.TenantId == tid && o.ExpiresAt < now).ExecuteDeleteAsync(ct);
    }

    public Task<int> PurgeOldLoginHistoryAsync(DateTimeOffset cutoff, CancellationToken ct = default)
    {
        var tid = _tenant.TenantId;
        return _db.LoginHistory.Where(h => h.TenantId == tid && h.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<Guid>> ListAllTenantIdsAsync(CancellationToken ct = default)
        => await _db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);

    public Task BindTenantScopeAsync(Guid tenantId, CancellationToken ct = default)
    {
        _tenant.SetTenant(tenantId);
        return Task.CompletedTask;
    }
}
