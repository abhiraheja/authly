using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class ObservabilityConfigRepository : IObservabilityConfigRepository
{
    private readonly AppDbContext _db;

    public ObservabilityConfigRepository(AppDbContext db) => _db = db;

    public Task<ObservabilityConfig?> GetAsync(CancellationToken ct = default)
        => _db.ObservabilityConfigs.OrderBy(c => c.UpdatedAt).FirstOrDefaultAsync(ct);

    public async Task UpsertAsync(ObservabilityConfig config, CancellationToken ct = default)
    {
        var existing = await _db.ObservabilityConfigs.FirstOrDefaultAsync(ct);
        if (existing is null)
        {
            config.UpdatedAt = DateTimeOffset.UtcNow;
            _db.ObservabilityConfigs.Add(config);
        }
        else
        {
            existing.Enabled = config.Enabled;
            existing.Exporter = config.Exporter;
            existing.OtlpEndpoint = config.OtlpEndpoint;
            existing.OtlpHeadersEncrypted = config.OtlpHeadersEncrypted;
            existing.AzureConnectionStringEncrypted = config.AzureConnectionStringEncrypted;
            existing.Signals = config.Signals;
            existing.SamplingRatio = config.SamplingRatio;
            existing.LogStreamEndpoint = config.LogStreamEndpoint;
            existing.LogStreamKeyEncrypted = config.LogStreamKeyEncrypted;
            existing.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }
}
