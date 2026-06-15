namespace Authly.Modules.Claims;

/// <summary>
/// Assembles the tenant's custom claims for a token (§5.6 steps 2–4): static values, then mapped
/// user/app-metadata values, then claims merged from pre-token pipeline hooks. A blocking hook
/// failure surfaces as <see cref="ClaimAssemblyResult.Blocked"/> so the issuer can deny the token.
/// </summary>
public interface ITokenClaimAssembler
{
    Task<ClaimAssemblyResult> AssembleAsync(ClaimAssemblyRequest request, CancellationToken ct = default);
}
