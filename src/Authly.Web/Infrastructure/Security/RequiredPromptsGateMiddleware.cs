using System.Security.Claims;
using Authly.Core.Interfaces;
using Authly.Modules.Policies;
using Authly.Modules.Surveys;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication;

namespace Authly.Web.Infrastructure.Security;

/// <summary>
/// Sign-in enforcement gate. After authentication, for navigational requests into the end-user
/// surfaces (<c>/portal/*</c>) and the OIDC authorize endpoint (<c>/connect/authorize</c>), it checks
/// whether the signed-in user has any pending required policies for the current context and, if so,
/// redirects to the consent page (<c>/account/policies</c>) preserving the original destination.
/// Because every sign-in method lands here (and OIDC client logins pass through /connect/authorize),
/// no entry point can bypass an unaccepted mandatory policy — including a social login that pre-dated
/// the policy. Optional/skippable policies are shown too (skip is per-session).
/// </summary>
public sealed class RequiredPromptsGateMiddleware
{
    private readonly RequestDelegate _next;

    public RequiredPromptsGateMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, IUserPromptService prompts, ISurveyService surveys, IApplicationRepository applications)
    {
        if (!ShouldEvaluate(context.Request))
        {
            await _next(context);
            return;
        }

        var auth = await context.AuthenticateAsync(AuthSchemes.User);
        if (!auth.Succeeded || auth.Principal is null)
        {
            await _next(context);
            return;
        }

        var principal = auth.Principal;
        if (!Guid.TryParse(principal.FindFirstValue(UserClaims.TenantId), out var tenantId)
            || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            await _next(context);
            return;
        }

        Guid.TryParse(principal.FindFirstValue(UserClaims.SessionId), out var sessionId);
        var applicationId = await ResolveApplicationIdAsync(context, applications);

        var session = sessionId == Guid.Empty ? (Guid?)null : sessionId;
        var returnUrl = context.Request.Path + context.Request.QueryString;

        // Policies first (legal), then surveys.
        var pendingPolicies = await prompts.GetPendingAsync(tenantId, userId, session, applicationId, context.RequestAborted);
        if (pendingPolicies.Any)
        {
            context.Response.Redirect($"/account/policies?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        var pendingSurveys = await surveys.GetPendingAsync(tenantId, userId, session, applicationId, context.RequestAborted);
        if (pendingSurveys.Count > 0)
        {
            context.Response.Redirect($"/account/survey/{pendingSurveys[0].SurveyId}?returnUrl={Uri.EscapeDataString(returnUrl)}");
            return;
        }

        await _next(context);
    }

    /// <summary>Only gate top-level GET navigations into the protected surfaces; never the consent
    /// page itself, auth endpoints, APIs or static assets.</summary>
    private static bool ShouldEvaluate(HttpRequest request)
    {
        if (!HttpMethods.IsGet(request.Method)) return false;
        var path = request.Path;
        if (path.StartsWithSegments("/account")) return false; // login, logout, consent page, asset
        return path.StartsWithSegments("/portal")
            || path.Equals("/connect/authorize", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>On /connect/authorize the client_id query identifies the app being signed into —
    /// used for application-targeted policies. Null on the portal (no app context).</summary>
    private static async Task<Guid?> ResolveApplicationIdAsync(HttpContext context, IApplicationRepository applications)
    {
        if (!context.Request.Path.Equals("/connect/authorize", StringComparison.OrdinalIgnoreCase)) return null;
        var clientId = context.Request.Query["client_id"].ToString();
        if (string.IsNullOrWhiteSpace(clientId)) return null;
        var app = await applications.GetByClientIdAsync(clientId, context.RequestAborted);
        return app?.Id;
    }
}
