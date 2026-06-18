using Microsoft.AspNetCore.Mvc.Controllers;

namespace Authly.Web.Infrastructure.Security;

/// <summary>
/// Deployment gate for the public marketing website. When the <c>Website:Enabled</c> config flag is
/// <c>false</c> (the default; set in docker via the <c>Website__Enabled</c> env var) the marketing
/// surface — the <see cref="Controllers.HomeController"/> landing pages and the
/// <see cref="Controllers.DocsController"/> docs hub — is switched off and every one of its routes
/// redirects to the admin console at <c>/tenantadmin/applications</c> (which itself bounces through
/// login when the visitor is not authenticated). The console (login, OIDC <c>/connect/*</c>, and the
/// TenantAdmin/Portal areas) is unaffected, so a self-hosted instance exposes only the IdP/console.
///
/// <para>The decision keys off the matched endpoint's controller, not the URL, so it stays correct if
/// marketing routes are renamed. Must run after <c>UseRouting</c> so the endpoint is resolved.</para>
/// </summary>
public sealed class WebsiteGateMiddleware
{
    // Controllers that make up the public marketing website (everything else is "console").
    private static readonly HashSet<string> MarketingControllers =
        new(StringComparer.OrdinalIgnoreCase) { "Home", "Docs" };

    // Admin console entry point; redirecting an unauthenticated visitor here triggers the login challenge.
    private const string ConsolePath = "/tenantadmin/applications";

    private readonly RequestDelegate _next;
    private readonly bool _websiteEnabled;

    public WebsiteGateMiddleware(RequestDelegate next, IConfiguration configuration)
    {
        _next = next;
        // Default false: the marketing website is off unless a deployment explicitly turns it on.
        _websiteEnabled = configuration.GetValue("Website:Enabled", false);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!_websiteEnabled)
        {
            var controller = context.GetEndpoint()?.Metadata
                .GetMetadata<ControllerActionDescriptor>()?.ControllerName;

            if (controller is not null && MarketingControllers.Contains(controller))
            {
                context.Response.Redirect(ConsolePath);
                return;
            }
        }

        await _next(context);
    }
}
