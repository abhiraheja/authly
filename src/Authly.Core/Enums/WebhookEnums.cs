namespace Authly.Core.Enums;

/// <summary>Lifecycle of a single webhook delivery attempt sequence (§4.12).</summary>
public enum WebhookDeliveryStatus
{
    /// <summary>Queued or mid-retry; a dispatch is scheduled.</summary>
    Pending,

    /// <summary>The endpoint acknowledged with a 2xx response.</summary>
    Success,

    /// <summary>All retry attempts were exhausted without a 2xx.</summary>
    Failed
}

/// <summary>
/// A point in an auth flow where a tenant may run a synchronous outbound hook (§4.12). Stages are
/// stored as snake_case text in <c>pipeline_hooks.stage</c>.
/// </summary>
public enum PipelineStage
{
    PreRegistration,
    PostRegistration,
    PreLogin,
    PostLogin,
    PreToken,
    SendOtp,
    SendEmail
}

/// <summary>What to do when a pipeline hook errors, times out, or is denied (§4.12).</summary>
public enum HookFailureMode
{
    /// <summary>Ignore the failure and continue the flow (fail-open).</summary>
    Continue,

    /// <summary>Abort the flow (fail-closed) — e.g. a fraud check that vetoes registration.</summary>
    Block
}

/// <summary>Which token a custom claim is written to (§4.13).</summary>
public enum ClaimTokenType
{
    Id,
    Access
}

/// <summary>How a custom claim's value is produced (§4.13 / §5.6).</summary>
public enum ClaimSourceType
{
    /// <summary>A literal value taken verbatim from <c>source</c>.</summary>
    Static,

    /// <summary>A dotted path resolved against the user's <c>user_metadata</c>/<c>app_metadata</c>.</summary>
    Metadata,

    /// <summary>A value fetched by POSTing to the URL in <c>source</c> (signed, timeout-bounded).</summary>
    Webhook
}
