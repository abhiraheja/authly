using Authly.Core.Entities;
using Authly.Core.Events;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class WebhookEndpointRepository : IWebhookEndpointRepository
{
    private readonly AppDbContext _db;

    public WebhookEndpointRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<WebhookEndpoint>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.WebhookEndpoints
            .Where(w => w.TenantId == tenantId)
            .OrderByDescending(w => w.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WebhookEndpoint>> ListMatchingAsync(Guid tenantId, string eventName, CancellationToken ct = default)
        // Postgres array membership: subscribe to the exact event or the wildcard "*".
        => await _db.WebhookEndpoints
            .Where(w => w.TenantId == tenantId && w.IsActive
                && (w.Events.Contains(eventName) || w.Events.Contains(EventCatalog.Wildcard)))
            .ToListAsync(ct);

    public Task<WebhookEndpoint?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.WebhookEndpoints.FirstOrDefaultAsync(w => w.TenantId == tenantId && w.Id == id, ct);

    public async Task AddAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        _db.WebhookEndpoints.Add(endpoint);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        _db.WebhookEndpoints.Update(endpoint);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(WebhookEndpoint endpoint, CancellationToken ct = default)
    {
        _db.WebhookEndpoints.Remove(endpoint);
        await _db.SaveChangesAsync(ct);
    }
}
