using Authly.Core.Compliance;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace Authly.Tests.Compliance;

internal sealed class FakeConsentRepo : IConsentRecordRepository
{
    public readonly List<ConsentRecord> Items = new();
    public Task<IReadOnlyList<ConsentRecord>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<ConsentRecord>>(
            Items.Where(c => c.TenantId == tenantId && c.UserId == userId).ToList());
    public Task AddAsync(ConsentRecord record, CancellationToken ct = default) { Items.Add(record); return Task.CompletedTask; }
}

internal sealed class FakeComplianceStore : IComplianceDataStore
{
    public UserDataExport? ExportResult;
    public bool EraseResult = true;
    public int EraseCalls;

    public Task<UserDataExport?> ExportUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(ExportResult);
    public Task<bool> EraseUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        EraseCalls++;
        return Task.FromResult(EraseResult);
    }
}

internal sealed class RecordingAudit : IAuditLogger
{
    public readonly List<string> Events = new();
    public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
        Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
    { Events.Add(@event); return Task.CompletedTask; }
}
