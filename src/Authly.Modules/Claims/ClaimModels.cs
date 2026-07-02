using Authly.Core.Enums;

namespace Authly.Modules.Claims;

/// <summary>Admin form payload for creating a custom-claim config (§4.13).</summary>
public sealed class ClaimConfigInput
{
    public Guid? Id { get; set; }

    /// <summary>Target application, or null for tenant-wide.</summary>
    public Guid? ApplicationId { get; set; }

    public ClaimTokenType TokenType { get; set; } = ClaimTokenType.Access;
    public ClaimSourceType Type { get; set; } = ClaimSourceType.Static;
    public string ClaimName { get; set; } = "";

    /// <summary>Static value or metadata path (interpreted per <see cref="Type"/>).</summary>
    public string? Source { get; set; }
}

/// <summary>Inputs to claim assembly (§5.6 steps 2–4) for a single token issuance.</summary>
/// <param name="TenantId">Issuing tenant.</param>
/// <param name="ApplicationId">Issuing application (null for client-credentials with no app context).</param>
/// <param name="TokenType">Which token is being assembled.</param>
/// <param name="UserMetadataJson">The subject's <c>user_metadata</c> JSON, if any.</param>
/// <param name="AppMetadataJson">The subject's <c>app_metadata</c> JSON, if any.</param>
/// <param name="HookPayload">Body posted to pre-token hooks (subject id, email, scopes, …).</param>
/// <param name="RunPreTokenHooks">
/// Whether to invoke pre-token pipeline hooks (§5.6 step 4). Issuers assembling both token types in
/// one pass should run hooks for one type only so the hooks fire exactly once per token request.
/// </param>
/// <param name="HookClaims">
/// Claims produced by the pre-token hook on the other pass. Lets a <see cref="ClaimSourceType.Hook"/>
/// config emit a hook claim to this token type without re-running the hook (e.g. routing a hook
/// claim into the id_token while the hook fires only on the access pass).
/// </param>
public sealed record ClaimAssemblyRequest(
    Guid TenantId,
    Guid? ApplicationId,
    ClaimTokenType TokenType,
    string? UserMetadataJson,
    string? AppMetadataJson,
    object HookPayload,
    bool RunPreTokenHooks = true,
    IReadOnlyDictionary<string, string>? HookClaims = null);

/// <summary>Outcome of claim assembly.</summary>
/// <param name="Claims">Custom claims to add to the token.</param>
/// <param name="Blocked">A blocking pre-token hook vetoed issuance.</param>
/// <param name="BlockReason">Why issuance was blocked, if so.</param>
/// <param name="HookClaimNames">
/// Which of <paramref name="Claims"/> originated from a pre-token pipeline hook (as opposed to
/// static/metadata config). A hook is a trusted, tenant-scoped extension point, so the issuer may
/// let it contribute authorization claims (roles/permissions) that a plain custom claim cannot.
/// Never contains protocol/identity claims. Null/empty when no hook claims were produced.
/// </param>
public sealed record ClaimAssemblyResult(
    IReadOnlyDictionary<string, string> Claims,
    bool Blocked = false,
    string? BlockReason = null,
    IReadOnlySet<string>? HookClaimNames = null);

/// <summary>Thrown when a claim config is invalid.</summary>
public sealed class ClaimConfigInvalidException : Exception
{
    public ClaimConfigInvalidException(string message) : base(message) { }
}
