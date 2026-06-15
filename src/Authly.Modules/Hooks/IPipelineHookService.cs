using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Modules.Common;

namespace Authly.Modules.Hooks;

/// <summary>
/// Tenant pipeline-hook administration plus synchronous stage execution (§4.12).
/// </summary>
public interface IPipelineHookService
{
    // --- Admin ---
    Task<IReadOnlyList<PipelineHook>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<PipelineHook?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task SaveAsync(Guid tenantId, PipelineHookInput input, AuditContext actor, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    /// <summary>
    /// Run every active hook bound to <paramref name="stage"/> in order, each signed and
    /// timeout-bounded. Stops and returns a blocked result on the first <c>block</c>-mode failure;
    /// otherwise continues and merges all hook responses for claim injection.
    /// </summary>
    Task<PipelineStageResult> RunStageAsync(PipelineStage stage, Guid tenantId, object payload, CancellationToken ct = default);
}
