using System.Security.Claims;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Modules.Policies;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// The consent wall an end-user is redirected to (by <c>RequiredPromptsGateMiddleware</c>) when they
/// have pending required policies. Shows each policy's content (HTML or embedded PDF), records the
/// accept/skip/reject decision, then resumes the original destination once nothing is left blocking.
/// </summary>
[Authorize(Policy = AuthPolicies.User)]
[Route("account/policies")]
public sealed class PoliciesController : Controller
{
    private readonly IUserPromptService _prompts;
    private readonly IPolicyService _policyService;
    private readonly IApplicationRepository _applications;

    public PoliciesController(IUserPromptService prompts, IPolicyService policyService, IApplicationRepository applications)
    {
        _prompts = prompts;
        _policyService = policyService;
        _applications = applications;
    }

    private Guid TenantId => Guid.Parse(User.FindFirstValue(UserClaims.TenantId)!);
    private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private Guid? CurrentSessionId =>
        Guid.TryParse(User.FindFirstValue(UserClaims.SessionId), out var id) ? id : null;

    [HttpGet("")]
    public async Task<IActionResult> Index(string? returnUrl, CancellationToken ct)
    {
        var safeReturn = SafeReturnUrl(returnUrl);
        var appId = await ResolveApplicationIdAsync(safeReturn, ct);

        var pending = await _prompts.GetPendingAsync(TenantId, UserId, CurrentSessionId, appId, ct);
        if (!pending.Any)
            return Redirect(safeReturn ?? Url.Action("Index", "Profile", new { area = "Portal" })!);

        ViewData["Title"] = "Before you continue";
        ViewData["ReturnUrl"] = safeReturn;
        return View(pending);
    }

    [HttpPost("decide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decide(Guid policyId, string decision, string? returnUrl, CancellationToken ct)
    {
        var safeReturn = SafeReturnUrl(returnUrl);
        var appId = await ResolveApplicationIdAsync(safeReturn, ct);

        if (TryParseDecision(decision, out var decisionType))
            await _prompts.RecordDecisionAsync(TenantId, UserId, policyId, decisionType, CurrentSessionId, appId, CurrentAudit(), ct);

        return RedirectToAction(nameof(Index), new { returnUrl = safeReturn });
    }

    /// <summary>Declining a mandatory policy: record the rejection and sign out (no access without it).</summary>
    [HttpPost("decline")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Decline(Guid policyId, CancellationToken ct)
    {
        await _prompts.RecordDecisionAsync(TenantId, UserId, policyId, PolicyDecisionType.Rejected,
            CurrentSessionId, null, CurrentAudit(), ct);
        await HttpContext.SignOutAsync(AuthSchemes.User);
        return RedirectToAction("Login", "Account");
    }

    /// <summary>Serves an uploaded policy PDF inline (tenant-scoped; queried by unguessable id).</summary>
    [HttpGet("asset/{id:guid}")]
    public async Task<IActionResult> Asset(Guid id, CancellationToken ct)
    {
        var asset = await _policyService.GetAssetAsync(id, ct);
        if (asset is null || asset.TenantId != TenantId) return NotFound();
        Response.Headers.ContentDisposition = $"inline; filename=\"{asset.FileName}\"";
        return File(asset.Data, asset.ContentType);
    }

    private static bool TryParseDecision(string? value, out PolicyDecisionType decision)
    {
        decision = (value ?? "").Trim().ToLowerInvariant() switch
        {
            "accept" => PolicyDecisionType.Accepted,
            "reject" => PolicyDecisionType.Rejected,
            "skip" => PolicyDecisionType.Skipped,
            _ => default
        };
        return value is "accept" or "reject" or "skip";
    }

    private async Task<Guid?> ResolveApplicationIdAsync(string? returnUrl, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(returnUrl)) return null;
        // returnUrl is a local path like "/connect/authorize?client_id=...&...".
        var qIndex = returnUrl.IndexOf('?');
        if (qIndex < 0 || !returnUrl.StartsWith("/connect/authorize", StringComparison.OrdinalIgnoreCase)) return null;
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(returnUrl[qIndex..]);
        if (!query.TryGetValue("client_id", out var clientId) || string.IsNullOrWhiteSpace(clientId)) return null;
        var app = await _applications.GetByClientIdAsync(clientId!, ct);
        return app?.Id;
    }

    private string? SafeReturnUrl(string? url)
        => !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? url : null;

    private AuditContext CurrentAudit() => new(
        UserId, "user",
        HttpContext.Connection.RemoteIpAddress?.ToString(),
        Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
