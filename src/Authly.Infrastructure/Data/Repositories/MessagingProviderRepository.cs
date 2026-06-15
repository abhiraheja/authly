using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class MessagingProviderRepository : IMessagingProviderRepository
{
    private readonly AppDbContext _db;

    public MessagingProviderRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<MessagingProvider>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.MessagingProviders
            .Where(p => p.TenantId == tenantId)
            .OrderBy(p => p.Channel).ThenBy(p => p.Provider)
            .ToListAsync(ct);

    public Task<MessagingProvider?> GetActiveAsync(Guid tenantId, MessageChannel channel, CancellationToken ct = default)
        => _db.MessagingProviders
            .Where(p => p.TenantId == tenantId && p.Channel == channel && p.IsActive)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(ct);

    public Task<MessagingProvider?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.MessagingProviders.FirstOrDefaultAsync(p => p.TenantId == tenantId && p.Id == id, ct);

    public async Task AddAsync(MessagingProvider provider, CancellationToken ct = default)
    {
        _db.MessagingProviders.Add(provider);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MessagingProvider provider, CancellationToken ct = default)
    {
        _db.MessagingProviders.Update(provider);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(MessagingProvider provider, CancellationToken ct = default)
    {
        _db.MessagingProviders.Remove(provider);
        await _db.SaveChangesAsync(ct);
    }
}
