using Authly.Modules.Common;

namespace Authly.Modules.Security;

/// <summary>Reads/writes the per-tenant <see cref="TenantSecuritySettings"/> (the "security" node of tenants.settings).</summary>
public interface ISecuritySettingsService
{
    Task<TenantSecuritySettings> GetAsync(Guid tenantId, CancellationToken ct = default);

    /// <summary>
    /// Persists settings. <paramref name="newCaptchaSecret"/>, when non-null/non-blank, is encrypted
    /// and stored; when null the existing encrypted secret is preserved (write-only field).
    /// </summary>
    Task SaveAsync(Guid tenantId, TenantSecuritySettings settings, string? newCaptchaSecret, AuditContext actor, CancellationToken ct = default);

    /// <summary>Decrypts the stored CAPTCHA secret for verification, or null when unset/undecryptable.</summary>
    string? DecryptCaptchaSecret(TenantSecuritySettings settings);
}
