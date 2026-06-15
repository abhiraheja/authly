using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.ApiKeys;

/// <summary>
/// Manages tenant API keys for the Management API: mint (raw shown once, stored hashed), list,
/// and revoke. All operations are tenant-scoped.
/// </summary>
public interface IApiKeyService
{
    Task<ApiKeyResult> CreateAsync(Guid tenantId, CreateApiKeyRequest request, AuditContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKey>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task RevokeAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);
}
