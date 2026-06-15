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
public sealed record ClaimAssemblyRequest(
    Guid TenantId,
    Guid? ApplicationId,
    ClaimTokenType TokenType,
    string? UserMetadataJson,
    string? AppMetadataJson,
    object HookPayload,
    bool RunPreTokenHooks = true);

/// <summary>Outcome of claim assembly.</summary>
/// <param name="Claims">Custom claims to add to the token.</param>
/// <param name="Blocked">A blocking pre-token hook vetoed issuance.</param>
/// <param name="BlockReason">Why issuance was blocked, if so.</param>
public sealed record ClaimAssemblyResult(
    IReadOnlyDictionary<string, string> Claims,
    bool Blocked = false,
    string? BlockReason = null);

/// <summary>Thrown when a claim config is invalid.</summary>
public sealed class ClaimConfigInvalidException : Exception
{
    public ClaimConfigInvalidException(string message) : base(message) { }
}
