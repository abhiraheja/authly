using Authly.Core.Enums;

namespace Authly.Modules.Hooks;

/// <summary>Admin form payload for creating/updating a pipeline hook.</summary>
public sealed class PipelineHookInput
{
    public Guid? Id { get; set; }
    public PipelineStage Stage { get; set; }
    public string Url { get; set; } = "";

    /// <summary>New signing secret; blank on edit means keep the existing one.</summary>
    public string? Secret { get; set; }

    public int TimeoutMs { get; set; } = 3000;
    public HookFailureMode OnFailure { get; set; } = HookFailureMode.Continue;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Result of running all hooks bound to a stage. <see cref="Blocked"/> is set when a hook with
/// <see cref="HookFailureMode.Block"/> failed; the flow must abort. <see cref="MergedData"/> holds
/// the union of the hooks' JSON-object responses (used to inject claims at the pre-token stage).
/// </summary>
public sealed class PipelineStageResult
{
    public bool Blocked { get; init; }
    public string? BlockReason { get; init; }
    public IReadOnlyDictionary<string, string> MergedData { get; init; } = new Dictionary<string, string>();

    public static readonly PipelineStageResult Continue = new();
    public static PipelineStageResult Block(string reason) => new() { Blocked = true, BlockReason = reason };
}

/// <summary>Thrown when a hook config is missing required fields.</summary>
public sealed class PipelineHookConfigInvalidException : Exception
{
    public PipelineHookConfigInvalidException(string message) : base(message) { }
}
