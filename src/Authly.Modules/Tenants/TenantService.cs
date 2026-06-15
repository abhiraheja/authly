using System.Text;
using System.Text.RegularExpressions;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Tenants;

/// <inheritdoc />
public sealed partial class TenantService : ITenantService
{
    private readonly ITenantRepository _repo;
    private readonly IAuditLogger _audit;

    public TenantService(ITenantRepository repo, IAuditLogger audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default) => _repo.ListAsync(ct);

    public Task<Tenant?> GetAsync(Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(id, ct);

    public async Task<Tenant> CreateAsync(CreateTenantRequest request, AuditContext actor, CancellationToken ct = default)
    {
        var slug = Slugify(string.IsNullOrWhiteSpace(request.Slug) ? request.Name : request.Slug!);
        if (await _repo.SlugExistsAsync(slug, ct))
            throw new SlugAlreadyExistsException(slug);

        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant
        {
            Name = request.Name.Trim(),
            Slug = slug,
            Status = TenantStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        await _repo.AddAsync(tenant, ct);
        await _audit.LogAsync("tenant.created", actor, tenantId: tenant.Id,
            resourceType: "tenant", resourceId: tenant.Id,
            metadata: new { tenant.Slug, tenant.Name }, ct: ct);

        return tenant;
    }

    public Task SuspendAsync(Guid id, AuditContext actor, CancellationToken ct = default)
        => ChangeStatusAsync(id, TenantStatus.Suspended, "tenant.suspended", actor, ct);

    public Task ReactivateAsync(Guid id, AuditContext actor, CancellationToken ct = default)
        => ChangeStatusAsync(id, TenantStatus.Active, "tenant.reactivated", actor, ct);

    public Task DeleteAsync(Guid id, AuditContext actor, CancellationToken ct = default)
        => ChangeStatusAsync(id, TenantStatus.Deleted, "tenant.deleted", actor, ct);

    private async Task ChangeStatusAsync(Guid id, TenantStatus status, string @event, AuditContext actor, CancellationToken ct)
    {
        var tenant = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Tenant {id} not found.");

        tenant.Status = status;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(tenant, ct);

        await _audit.LogAsync(@event, actor, tenantId: tenant.Id,
            resourceType: "tenant", resourceId: tenant.Id,
            metadata: new { status = status.ToString() }, ct: ct);
    }

    /// <summary>Lower-cases, strips accents/punctuation, collapses to hyphen-separated slug.</summary>
    public static string Slugify(string input)
    {
        var lowered = input.Trim().ToLowerInvariant();
        var ascii = NonAscii().Replace(lowered, "-");
        var collapsed = MultiHyphen().Replace(ascii, "-").Trim('-');
        return collapsed.Length == 0 ? "tenant" : collapsed;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonAscii();

    [GeneratedRegex("-{2,}")]
    private static partial Regex MultiHyphen();
}
