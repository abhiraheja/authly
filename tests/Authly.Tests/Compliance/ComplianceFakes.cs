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

internal sealed class FakeInstanceRepo : ISelfHostedInstanceRepository
{
    public readonly List<SelfHostedInstance> Items = new();
    public Task<SelfHostedInstance?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(i => i.Id == id));
    public Task<SelfHostedInstance?> GetBySyncKeyHashAsync(string syncKeyHash, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(i => i.SyncKeyHash == syncKeyHash));
    public Task<IReadOnlyList<SelfHostedInstance>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<SelfHostedInstance>>(Items.ToList());
    public Task AddAsync(SelfHostedInstance instance, CancellationToken ct = default)
    {
        if (instance.Id == Guid.Empty) instance.Id = Guid.NewGuid();
        Items.Add(instance);
        return Task.CompletedTask;
    }
    public Task UpdateAsync(SelfHostedInstance instance, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(SelfHostedInstance instance, CancellationToken ct = default) { Items.Remove(instance); return Task.CompletedTask; }
}

// Deterministic token hashing for tests: Hash is a stable, distinct-from-raw transform.
internal sealed class FakeTokenHasher : ITokenHasher
{
    private int _n;
    public string GenerateRawToken() => $"raw-{++_n}";
    public string Hash(string rawToken) => $"sha256({rawToken})";
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

// Minimal IConfiguration over a dictionary — DeploymentContext only uses the string indexer.
internal sealed class FakeConfig : IConfiguration
{
    private readonly Dictionary<string, string?> _d;
    public FakeConfig(Dictionary<string, string?> d) => _d = d;
    public string? this[string key] { get => _d.GetValueOrDefault(key); set => _d[key] = value; }
    public IEnumerable<IConfigurationSection> GetChildren() => Array.Empty<IConfigurationSection>();
    public IChangeToken GetReloadToken() => throw new NotSupportedException();
    public IConfigurationSection GetSection(string key) => throw new NotSupportedException();
}
