using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class WebhookDeliveryRepository : IWebhookDeliveryRepository
{
    private readonly AppDbContext _db;

    public WebhookDeliveryRepository(AppDbContext db) => _db = db;

    public async Task AddAsync(WebhookDelivery delivery, CancellationToken ct = default)
    {
        _db.WebhookDeliveries.Add(delivery);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(WebhookDelivery delivery, CancellationToken ct = default)
    {
        _db.WebhookDeliveries.Update(delivery);
        await _db.SaveChangesAsync(ct);
    }

    // No tenant filter: the dispatch job runs outside a request and has already set the RLS scope
    // (app.current_tenant) from the tenant id carried on the queue, so the row is still isolated.
    public Task<WebhookDelivery?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.WebhookDeliveries.FirstOrDefaultAsync(d => d.Id == id, ct);

    public async Task<IReadOnlyList<WebhookDelivery>> ListRecentByTenantAsync(Guid tenantId, int take, CancellationToken ct = default)
        => await _db.WebhookDeliveries
            .Where(d => d.TenantId == tenantId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(take)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<WebhookDelivery>> ListByEndpointAsync(Guid tenantId, Guid endpointId, int take, CancellationToken ct = default)
        => await _db.WebhookDeliveries
            .Where(d => d.TenantId == tenantId && d.EndpointId == endpointId)
            .OrderByDescending(d => d.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
}
