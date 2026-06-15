using Authly.Core.Compliance;

namespace Authly.Web.Infrastructure.Maintenance;

/// <summary>
/// Recurring data-retention / cleanup jobs (§GDPR retention). Hourly: purge expired transient
/// credentials (verification/reset tokens, OTP codes). Daily: purge stale login history and old
/// audit logs past their retention windows. Tenant-scoped tables are purged per tenant so the RLS
/// backstop lets the deletes through; non-tenant tables are cleared globally.
/// </summary>
public sealed class MaintenanceJobs
{
    private readonly IRetentionStore _store;
    private readonly ILogger<MaintenanceJobs> _logger;
    private readonly int _loginHistoryDays;
    private readonly int _auditDays;

    public MaintenanceJobs(IRetentionStore store, IConfiguration config, ILogger<MaintenanceJobs> logger)
    {
        _store = store;
        _logger = logger;
        _loginHistoryDays = ReadDays(config["RETENTION_LOGIN_HISTORY_DAYS"], 90);
        _auditDays = ReadDays(config["RETENTION_AUDIT_DAYS"], 365);
    }

    /// <summary>Hourly: clear expired single-use credentials.</summary>
    public async Task ExpireTransientCredentialsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var verif = await _store.PurgeExpiredVerificationTokensAsync(now, ct);
        var reset = await _store.PurgeExpiredPasswordResetTokensAsync(now, ct);

        var otp = 0;
        foreach (var tenantId in await _store.ListAllTenantIdsAsync(ct))
        {
            await _store.BindTenantScopeAsync(tenantId, ct);
            otp += await _store.PurgeExpiredOtpCodesAsync(now, ct);
        }

        _logger.LogInformation(
            "Retention (hourly): purged {Verif} verification tokens, {Reset} reset tokens, {Otp} OTP codes.",
            verif, reset, otp);
    }

    /// <summary>Daily: purge history past its retention window.</summary>
    public async Task PurgeStaleHistoryAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var loginCutoff = now.AddDays(-_loginHistoryDays);
        var login = 0;
        foreach (var tenantId in await _store.ListAllTenantIdsAsync(ct))
        {
            await _store.BindTenantScopeAsync(tenantId, ct);
            login += await _store.PurgeOldLoginHistoryAsync(loginCutoff, ct);
        }

        var audit = await _store.PurgeOldAuditLogsAsync(now.AddDays(-_auditDays), ct);

        _logger.LogInformation(
            "Retention (daily): purged {Login} login-history rows (>{LoginDays}d), {Audit} audit rows (>{AuditDays}d).",
            login, _loginHistoryDays, audit, _auditDays);
    }

    private static int ReadDays(string? value, int fallback)
        => int.TryParse(value, out var d) && d > 0 ? d : fallback;
}
