using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Claims;

/// <summary>Tenant administration of custom token-claim configuration (§4.13).</summary>
public interface IClaimConfigService
{
    Task<IReadOnlyList<ClaimConfig>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task AddAsync(Guid tenantId, ClaimConfigInput input, AuditContext actor, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);
}
