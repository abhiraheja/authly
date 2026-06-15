using Authly.Core.Compliance;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Compliance;

public sealed class DataRightsService : IDataRightsService
{
    private readonly IComplianceDataStore _store;
    private readonly IAuditLogger _audit;

    public DataRightsService(IComplianceDataStore store, IAuditLogger audit)
    {
        _store = store;
        _audit = audit;
    }

    public async Task<UserDataExport?> ExportAsync(Guid tenantId, Guid userId, AuditContext actor, CancellationToken ct = default)
    {
        var export = await _store.ExportUserAsync(tenantId, userId, ct);
        if (export is null) return null;

        await _audit.LogAsync("user.data_exported", actor, tenantId: tenantId,
            resourceType: "user", resourceId: userId, ct: ct);
        return export;
    }

    public async Task<bool> EraseAsync(Guid tenantId, Guid userId, AuditContext actor, CancellationToken ct = default)
    {
        var erased = await _store.EraseUserAsync(tenantId, userId, ct);
        if (!erased) return false;

        // The user row is gone; the audit entry carries ids only — no residual PII.
        await _audit.LogAsync("user.erased", actor, tenantId: tenantId,
            resourceType: "user", resourceId: userId, ct: ct);
        return true;
    }
}
