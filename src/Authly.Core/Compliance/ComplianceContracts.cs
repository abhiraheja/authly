namespace Authly.Core.Compliance;

// ---------------------------------------------------------------------------
// Data subject export (GDPR Art. 15/20, DPDP access). Aggregated from tenant-scoped
// queries; serialized to JSON in the module layer. Contains the user's OWN data only.
// ---------------------------------------------------------------------------

public sealed record ExportedProfile(
    Guid Id, string Email, bool EmailVerified, string? Username, string? Phone, bool PhoneVerified,
    string Status, string? FirstName, string? LastName, string Timezone, string Locale,
    string UserMetadata, DateTimeOffset CreatedAt, DateTimeOffset? LastLoginAt);

public sealed record ExportedSession(
    Guid Id, string? IpAddress, string? UserAgent, string? Location, bool Trusted,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, bool Revoked);

public sealed record ExportedLogin(
    string Result, string? Method, string? IpAddress, string? UserAgent, string? Location,
    string? Reason, DateTimeOffset CreatedAt);

public sealed record ExportedMfaFactor(string Type, string Status, string? FriendlyName, DateTimeOffset CreatedAt);

public sealed record ExportedSocialIdentity(string Provider, string? ProviderEmail, DateTimeOffset CreatedAt);

public sealed record ExportedRecoveryContact(string Type, string Value, bool Verified, DateTimeOffset CreatedAt);

public sealed record ExportedConsent(string Purpose, bool Granted, string? Version, DateTimeOffset CreatedAt);

/// <summary>The complete portable export for one user. No password hashes, no MFA secrets, no tokens.</summary>
public sealed record UserDataExport(
    DateTimeOffset GeneratedAt,
    ExportedProfile Profile,
    IReadOnlyList<string> Roles,
    IReadOnlyList<ExportedSession> Sessions,
    IReadOnlyList<ExportedLogin> LoginHistory,
    IReadOnlyList<ExportedMfaFactor> MfaFactors,
    IReadOnlyList<ExportedSocialIdentity> SocialIdentities,
    IReadOnlyList<ExportedRecoveryContact> RecoveryContacts,
    IReadOnlyList<ExportedConsent> Consents);

/// <summary>
/// Reads and erases all of a single user's data within their tenant. Lives in Infrastructure
/// because it spans many tables via direct EF access; the Compliance module orchestrates it.
/// Every query is tenant-scoped (and RLS-backstopped).
/// </summary>
public interface IComplianceDataStore
{
    Task<UserDataExport?> ExportUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Hard-deletes the user (cascading to sessions, login history, MFA, social, recovery,
    /// consent, tokens, etc.). Returns false if the user doesn't exist in the tenant.
    /// </summary>
    Task<bool> EraseUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Self-host telemetry — AGGREGATE COUNTS ONLY. Never user records / emails / tokens.
// ---------------------------------------------------------------------------

public sealed record InstanceMetrics(int TenantCount, int UserCount, int AppCount, int ActiveSessionCount);

/// <summary>
/// Computes platform-wide aggregate counts for the self-host telemetry push. Crosses tenants
/// (sums per-tenant counts behind RLS); emits numbers only — no identifying data of any kind.
/// </summary>
public interface IInstanceMetricsCollector
{
    Task<InstanceMetrics> CollectAsync(CancellationToken ct = default);
}

// ---------------------------------------------------------------------------
// Data retention / cleanup (recurring maintenance). Bulk deletes by time.
// ---------------------------------------------------------------------------

/// <summary>
/// Bulk purge of expired/stale rows for the retention jobs. Some targets are RLS-protected and
/// must be purged within a tenant scope — the caller binds <see cref="BindTenantScopeAsync"/>
/// (or iterates tenants via <see cref="ListAllTenantIdsAsync"/>) before calling those methods.
/// Non-tenant-scoped targets (tokens without a tenant column, audit logs) purge globally.
/// </summary>
public interface IRetentionStore
{
    // --- global (no RLS) — one statement clears every tenant ---
    Task<int> PurgeExpiredVerificationTokensAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<int> PurgeExpiredPasswordResetTokensAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<int> PurgeOldAuditLogsAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    // --- tenant-scoped (RLS) — bind a tenant scope first ---
    Task<int> PurgeExpiredOtpCodesAsync(DateTimeOffset now, CancellationToken ct = default);
    Task<int> PurgeOldLoginHistoryAsync(DateTimeOffset cutoff, CancellationToken ct = default);

    /// <summary>All tenant ids (the tenants table is not RLS-protected).</summary>
    Task<IReadOnlyList<Guid>> ListAllTenantIdsAsync(CancellationToken ct = default);

    /// <summary>
    /// Explicitly sets <c>app.current_tenant</c> on the current connection so the subsequent
    /// RLS-scoped purge runs against exactly that tenant — robust against a pooled/kept-open
    /// connection holding a stale value across a loop.
    /// </summary>
    Task BindTenantScopeAsync(Guid tenantId, CancellationToken ct = default);
}
