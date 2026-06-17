using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Data.Repositories;

public sealed class ApplicationRepository : IApplicationRepository
{
    private readonly AppDbContext _db;

    public ApplicationRepository(AppDbContext db) => _db = db;

    public Task<Application?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _db.Applications.FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == id, ct);

    public Task<Application?> GetByClientIdAsync(string clientId, CancellationToken ct = default)
        => _db.Applications.FirstOrDefaultAsync(a => a.ClientId == clientId, ct);

    public async Task<IReadOnlyList<Application>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => await _db.Applications
            .Where(a => a.TenantId == tenantId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<string>> ListAllRedirectUrisAsync(CancellationToken ct = default)
    {
        // text[] column — pull the arrays and flatten in memory (the applications table is small).
        var lists = await _db.Applications
            .Where(a => a.RedirectUris.Count > 0)
            .Select(a => a.RedirectUris)
            .ToListAsync(ct);
        return lists.SelectMany(uris => uris).ToList();
    }

    public async Task AddAsync(Application application, CancellationToken ct = default)
    {
        _db.Applications.Add(application);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Application application, CancellationToken ct = default)
    {
        _db.Applications.Update(application);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Application application, CancellationToken ct = default)
    {
        _db.Applications.Remove(application);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ApplicationSecret>> ListSecretsAsync(Guid applicationId, CancellationToken ct = default)
        => await _db.ApplicationSecrets
            .Where(s => s.ApplicationId == applicationId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async Task AddSecretAsync(ApplicationSecret secret, CancellationToken ct = default)
    {
        _db.ApplicationSecrets.Add(secret);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeSecretsAsync(Guid applicationId, CancellationToken ct = default)
        => await _db.ApplicationSecrets
            .Where(s => s.ApplicationId == applicationId && !s.Revoked)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Revoked, true), ct);
}
