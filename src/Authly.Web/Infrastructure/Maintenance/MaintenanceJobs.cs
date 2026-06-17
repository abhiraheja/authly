using Authly.Core.Compliance;
using Microsoft.Extensions.DependencyInjection;

namespace Authly.Web.Infrastructure.Maintenance;

/// <summary>
/// Recurring data-retention / cleanup jobs (§GDPR retention). Hourly: purge expired transient
/// credentials (verification/reset tokens, OTP codes). Daily: purge stale login history and old
/// audit logs past their retention windows. Tenant-scoped tables are purged per tenant so the RLS
/// backstop lets the deletes through; non-tenant tables are cleared globally.
///
/// Each tenant is processed in its own DI scope: the scoped <see cref="ITenantContext"/> is
/// set-once (a request-safety invariant), so a fresh scope per tenant gives each its own tenant
/// context and <c>DbContext</c> rather than trying to re-bind a single shared one.
/// </summary>
public sealed class MaintenanceJobs
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<MaintenanceJobs> _logger;
    private readonly int _loginHistoryDays;
    private readonly int _auditDays;

    public MaintenanceJobs(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<MaintenanceJobs> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _loginHistoryDays = ReadDays(config["RETENTION_LOGIN_HISTORY_DAYS"], 90);
        _auditDays = ReadDays(config["RETENTION_AUDIT_DAYS"], 365);
    }

    /// <summary>Hourly: clear expired single-use credentials.</summary>
    public async Task ExpireTransientCredentialsAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        int verif, reset;
        IReadOnlyList<Guid> tenantIds;
        using (var scope = _scopeFactory.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IRetentionStore>();
            verif = await store.PurgeExpiredVerificationTokensAsync(now, ct);
            reset = await store.PurgeExpiredPasswordResetTokensAsync(now, ct);
            tenantIds = await store.ListAllTenantIdsAsync(ct);
        }

        var otp = 0;
        foreach (var tenantId in tenantIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IRetentionStore>();
            await store.BindTenantScopeAsync(tenantId, ct);
            otp += await store.PurgeExpiredOtpCodesAsync(now, ct);
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

        IReadOnlyList<Guid> tenantIds;
        int audit;
        using (var scope = _scopeFactory.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IRetentionStore>();
            tenantIds = await store.ListAllTenantIdsAsync(ct);
            audit = await store.PurgeOldAuditLogsAsync(now.AddDays(-_auditDays), ct);
        }

        var login = 0;
        foreach (var tenantId in tenantIds)
        {
            using var scope = _scopeFactory.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IRetentionStore>();
            await store.BindTenantScopeAsync(tenantId, ct);
            login += await store.PurgeOldLoginHistoryAsync(loginCutoff, ct);
        }

        _logger.LogInformation(
            "Retention (daily): purged {Login} login-history rows (>{LoginDays}d), {Audit} audit rows (>{AuditDays}d).",
            login, _loginHistoryDays, audit, _auditDays);
    }

    private static int ReadDays(string? value, int fallback)
        => int.TryParse(value, out var d) && d > 0 ? d : fallback;
}
