using System.Text.Json;
using System.Text.Json.Nodes;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Security;

/// <inheritdoc />
public sealed class SecuritySettingsService : ISecuritySettingsService
{
    private const string Node = "security";

    private readonly ITenantRepository _tenants;
    private readonly IEncryptionService _encryption;
    private readonly IAuditLogger _audit;

    public SecuritySettingsService(ITenantRepository tenants, IEncryptionService encryption, IAuditLogger audit)
    {
        _tenants = tenants;
        _encryption = encryption;
        _audit = audit;
    }

    public async Task<TenantSecuritySettings> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        return ReadSettings(tenant?.Settings);
    }

    public async Task SaveAsync(Guid tenantId, TenantSecuritySettings settings, string? newCaptchaSecret, AuditContext actor, CancellationToken ct = default)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException($"Tenant '{tenantId}' not found.");

        // Preserve the existing encrypted secret unless a new one was supplied.
        var existing = ReadSettings(tenant.Settings);
        settings.CaptchaSecretEncrypted = string.IsNullOrWhiteSpace(newCaptchaSecret)
            ? existing.CaptchaSecretEncrypted
            : _encryption.Encrypt(newCaptchaSecret.Trim());

        var root = ParseObject(tenant.Settings);
        root[Node] = JsonSerializer.SerializeToNode(settings);
        tenant.Settings = root.ToJsonString();
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _tenants.UpdateAsync(tenant, ct);

        await _audit.LogAsync("security.settings_changed", actor, tenantId, "tenant", tenantId,
            metadata: new { settings.LockoutEnabled, settings.CaptchaEnabled, settings.BreachedPasswordCheck }, ct: ct);
    }

    public string? DecryptCaptchaSecret(TenantSecuritySettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.CaptchaSecretEncrypted)) return null;
        try { return _encryption.Decrypt(settings.CaptchaSecretEncrypted); }
        catch { return null; }
    }

    private static TenantSecuritySettings ReadSettings(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return new TenantSecuritySettings();
        try
        {
            var root = ParseObject(settingsJson);
            if (root[Node] is JsonNode node)
                return node.Deserialize<TenantSecuritySettings>() ?? new TenantSecuritySettings();
        }
        catch (JsonException) { /* malformed settings must not break security defaults */ }
        return new TenantSecuritySettings();
    }

    private static JsonObject ParseObject(string? json)
        => string.IsNullOrWhiteSpace(json) ? new JsonObject() : JsonNode.Parse(json) as JsonObject ?? new JsonObject();
}
