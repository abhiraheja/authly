using Authly.Core.Enums;

namespace Authly.Core.Entities;

/// <summary>
/// A synchronous outbound hook bound to an auth-flow <see cref="Stage"/> (§4.12). Invoked inline
/// with a short <see cref="TimeoutMs"/>; on error/timeout the flow follows <see cref="OnFailure"/>
/// (continue = fail-open, block = abort). <see cref="Secret"/> is AES-encrypted at rest and used to
/// HMAC-sign the request body. Maps to table "pipeline_hooks".
/// </summary>
public class PipelineHook
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }

    public PipelineStage Stage { get; set; }

    public string Url { get; set; } = default!;

    /// <summary>HMAC signing secret, AES-encrypted at rest.</summary>
    public string Secret { get; set; } = default!;

    public int TimeoutMs { get; set; } = 3000;

    public HookFailureMode OnFailure { get; set; } = HookFailureMode.Continue;

    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
}
