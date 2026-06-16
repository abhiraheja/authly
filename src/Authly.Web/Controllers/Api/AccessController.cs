using Authly.Core.Interfaces;
using Authly.Modules.Abac;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers.Api;

/// <summary>
/// ABAC policy decision point for integrators: ask whether a subject may perform an action on a
/// resource. Tenant-scoped from the API credential; evaluates the tenant's enabled policies.
/// </summary>
[Route("api/v1/access")]
public sealed class AccessController : ApiControllerBase
{
    private readonly IAuthorizationDecisionService _decisions;

    public AccessController(IAuthorizationDecisionService decisions, ITenantContext tenant) : base(tenant)
        => _decisions = decisions;

    public sealed record EvaluateRequest(
        string? Action,
        string? ResourceType,
        Dictionary<string, string>? Subject,
        Dictionary<string, string>? Resource,
        Dictionary<string, string>? Environment);

    public sealed record EvaluateResponse(bool Allowed, string? Policy, string Reason);

    [HttpPost("evaluate")]
    public async Task<IActionResult> Evaluate([FromBody] EvaluateRequest body, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(body.Action) || string.IsNullOrWhiteSpace(body.ResourceType))
            return ApiError(StatusCodes.Status400BadRequest, "invalid_request", "action and resourceType are required.");

        var request = new AccessRequest(
            body.Action.Trim(),
            body.ResourceType.Trim(),
            body.Subject ?? new(),
            body.Resource ?? new(),
            body.Environment ?? new());

        var decision = await _decisions.EvaluateAsync(TenantId, request, ct);
        return Ok(new EvaluateResponse(decision.Allowed, decision.PolicyName, decision.Reason));
    }
}
