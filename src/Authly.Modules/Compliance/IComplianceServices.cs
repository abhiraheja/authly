using Authly.Core.Compliance;
using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Compliance;

/// <summary>Captures and lists user consent (GDPR/DPDP consent tracking). Tenant-scoped.</summary>
public interface IConsentService
{
    /// <summary>Records that a user granted/withdrew consent for a purpose. Writes an audit entry.</summary>
    Task RecordAsync(Guid tenantId, Guid userId, string purpose, bool granted, string? version,
        AuditContext actor, CancellationToken ct = default);

    /// <summary>Records the standard signup consents (terms + privacy) granted together.</summary>
    Task RecordSignupConsentAsync(Guid tenantId, Guid userId, string? policyVersion,
        AuditContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<ConsentRecord>> ListAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
}

/// <summary>Data-subject rights: export (portability/access) and erasure. Tenant-scoped, ownership-guarded.</summary>
public interface IDataRightsService
{
    /// <summary>Assembles the user's complete data export, or null if the user doesn't exist in the tenant.</summary>
    Task<UserDataExport?> ExportAsync(Guid tenantId, Guid userId, AuditContext actor, CancellationToken ct = default);

    /// <summary>Permanently erases the user and their cascading data. Returns false if not found.</summary>
    Task<bool> EraseAsync(Guid tenantId, Guid userId, AuditContext actor, CancellationToken ct = default);
}

/// <summary>
/// Cloud control-plane side of self-host telemetry (§9): registers instances (issuing a one-time
/// sync key) and ingests their aggregate pushes. Platform-level, not tenant-scoped.
/// </summary>
public interface ISelfHostSyncService
{
    Task<InstanceRegistration> RegisterAsync(Guid? ownerTenantId, string? name, AuditContext actor, CancellationToken ct = default);

    Task<IReadOnlyList<SelfHostedInstance>> ListAsync(CancellationToken ct = default);

    /// <summary>
    /// Validates the presented sync key and records the aggregate metrics. Returns false for an
    /// unknown key (the caller responds 401). Never throws on a bad payload — telemetry is best-effort.
    /// </summary>
    Task<bool> IngestAsync(string rawSyncKey, SyncPayload payload, CancellationToken ct = default);
}
