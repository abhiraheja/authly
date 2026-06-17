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
