using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

/// <summary>Platform-level key/value store (no tenant scope / no RLS).</summary>
public sealed class PlatformStateStore : IPlatformStateStore
{
    private readonly AppDbContext _db;

    public PlatformStateStore(AppDbContext db) => _db = db;

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => (await _db.PlatformState.AsNoTracking().FirstOrDefaultAsync(s => s.Key == key, ct))?.Value;

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        var row = await _db.PlatformState.FirstOrDefaultAsync(s => s.Key == key, ct);
        if (row is null)
        {
            _db.PlatformState.Add(new PlatformState { Key = key, Value = value, UpdatedAt = DateTimeOffset.UtcNow });
        }
        else
        {
            row.Value = value;
            row.UpdatedAt = DateTimeOffset.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }
}
