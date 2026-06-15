using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class MessageTemplateRepository : IMessageTemplateRepository
{
    private readonly AppDbContext _db;

    public MessageTemplateRepository(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<MessageTemplate>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.MessageTemplates
            .Where(t => t.TenantId == tenantId)
            .OrderBy(t => t.Key).ThenBy(t => t.Channel).ThenBy(t => t.Locale)
            .ToListAsync(ct);

    public Task<MessageTemplate?> GetAsync(Guid tenantId, string key, MessageChannel channel, string locale, CancellationToken ct = default)
        => _db.MessageTemplates.FirstOrDefaultAsync(
            t => t.TenantId == tenantId && t.Key == key && t.Channel == channel && t.Locale == locale && t.IsActive, ct);

    public Task<MessageTemplate?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.MessageTemplates.FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == id, ct);

    public async Task AddAsync(MessageTemplate template, CancellationToken ct = default)
    {
        _db.MessageTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(MessageTemplate template, CancellationToken ct = default)
    {
        _db.MessageTemplates.Update(template);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(MessageTemplate template, CancellationToken ct = default)
    {
        _db.MessageTemplates.Remove(template);
        await _db.SaveChangesAsync(ct);
    }
}
