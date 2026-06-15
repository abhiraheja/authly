using System.Text.Json;
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

    public async Task<bool> IsOnboardedAsync(Guid id, CancellationToken ct = default)
    {
        var tenant = await _repo.GetByIdAsync(id, ct);
        return tenant is not null && ReadOnboardedFlag(tenant.Settings);
    }

    public async Task SetOnboardedAsync(Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var tenant = await _repo.GetByIdAsync(id, ct)
            ?? throw new KeyNotFoundException($"Tenant {id} not found.");
        if (ReadOnboardedFlag(tenant.Settings)) return;

        tenant.Settings = WriteOnboardedFlag(tenant.Settings);
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _repo.UpdateAsync(tenant, ct);

        await _audit.LogAsync("tenant.onboarded", actor, tenantId: tenant.Id,
            resourceType: "tenant", resourceId: tenant.Id, ct: ct);
    }

    // Settings is a free-form JSON object; we touch only the "onboarded" key and preserve the rest.
    private static bool ReadOnboardedFlag(string settingsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(settingsJson) ? "{}" : settingsJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("onboarded", out var v)
                && v.ValueKind == JsonValueKind.True;
        }
        catch (JsonException) { return false; }
    }

    private static string WriteOnboardedFlag(string settingsJson)
    {
        var map = new Dictionary<string, JsonElement>();
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(settingsJson) ? "{}" : settingsJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var p in doc.RootElement.EnumerateObject())
                    map[p.Name] = p.Value.Clone();
        }
        catch (JsonException) { /* start fresh on malformed settings */ }

        using var trueDoc = JsonDocument.Parse("true");
        map["onboarded"] = trueDoc.RootElement.Clone();
        return JsonSerializer.Serialize(map);
    }

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
