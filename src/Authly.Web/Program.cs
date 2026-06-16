using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Authly.Core.Interfaces;
using Authly.Infrastructure;
using Authly.Infrastructure.Data;
using Authly.Modules;
using Authly.Modules.Auth;
using Authly.Modules.SuperAdmins;
using Authly.Web.Infrastructure;
using Authly.Web.Infrastructure.Api;
using Authly.Web.Infrastructure.Messaging;
using Authly.Web.Infrastructure.OAuth;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// Deploy flag: when shipping/wrapping the product to customers, the platform super-admin surface
// is disabled so it is never exposed. Defaults to enabled for our own hosted deployment.
var superAdminEnabled = builder.Configuration.GetValue("SUPERADMIN_ENABLED", true);

// MVC + Razor (all panels, hosted login, end-user portal are server-rendered).
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Management API: error-envelope filter is resolved per request via [ServiceFilter].
builder.Services.AddScoped<ApiExceptionFilter>();

// MFA login gate: holds the half-authenticated state in a data-protected cookie.
builder.Services.AddSingleton<Authly.Web.Infrastructure.Mfa.MfaPendingStore>();

// Social login: protects the OAuth state payload (CSRF defence + tenant/redirect binding).
builder.Services.AddSingleton<Authly.Web.Infrastructure.Social.SocialStateProtector>();

// Per-tenant branding for the hosted login / portal layouts (request-scoped, resolved once).
builder.Services.AddScoped<Authly.Web.Infrastructure.Branding.CurrentBranding>();

// Passkeys / WebAuthn: relying-party (rpId/origin from request host) + the ceremony challenge cookie.
builder.Services.AddScoped<Authly.Core.WebAuthn.IWebAuthnRelyingParty, Authly.Web.Infrastructure.WebAuthn.WebAuthnRelyingParty>();
builder.Services.AddSingleton<Authly.Web.Infrastructure.WebAuthn.WebAuthnChallengeStore>();

// Security hardening (Phase 12): suspicious-login analysis job (runs via Hangfire) + view-state helper.
builder.Services.AddScoped<Authly.Web.Infrastructure.Security.SuspiciousLoginJob>();
builder.Services.AddScoped<Authly.Web.Infrastructure.Security.SecurityViewState>();

// Self-host & compliance (Phase 13): telemetry push job + retention/cleanup jobs (run via Hangfire).
builder.Services.AddScoped<Authly.Web.Infrastructure.SelfHost.SelfHostSyncJob>();
builder.Services.AddScoped<Authly.Web.Infrastructure.Maintenance.MaintenanceJobs>();
builder.Services.AddScoped<Authly.Web.Infrastructure.LogStreaming.LogStreamJob>();

// Infrastructure (EF Core, Redis, Argon2id, AES) + business modules.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddModules();

// Web-layer adapters for module/core abstractions (routing + Hangfire live here).
builder.Services.AddScoped<IAuthUrlBuilder, AuthUrlBuilder>();
builder.Services.AddScoped<IMessageQueue, HangfireMessageQueue>();
builder.Services.AddScoped<MessageDispatchJob>();
builder.Services.AddScoped<Authly.Core.Events.IWebhookQueue, Authly.Web.Infrastructure.Webhooks.HangfireWebhookQueue>();
builder.Services.AddScoped<Authly.Web.Infrastructure.Webhooks.WebhookDispatchJob>();

// OAuth 2.0 / OIDC server (OpenIddict) — endpoints, flows, dev signing keys.
builder.Services.AddAuthlyOpenIddict(builder.Environment.IsDevelopment());
builder.Services.AddScoped<IOAuthClientStore, OpenIddictClientStore>();

// Two fully isolated cookie schemes: platform super-admin and tenant end-users.
builder.Services.AddAuthentication(AuthSchemes.SuperAdmin)
    .AddCookie(AuthSchemes.SuperAdmin, options =>
    {
        options.Cookie.Name = "authly.superadmin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/superadmin/account/login";
        options.LogoutPath = "/superadmin/account/logout";
        options.AccessDeniedPath = "/superadmin/account/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddCookie(AuthSchemes.User, options =>
    {
        options.Cookie.Name = "authly.user";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
        // Re-check the backing session on each request so portal session-revoke / password-change take effect.
        options.Events.OnValidatePrincipal = SessionCookieValidator.ValidateAsync;
    })
    .AddCookie(AuthSchemes.TenantAdmin, options =>
    {
        options.Cookie.Name = "authly.tenantadmin";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/tenantadmin/account/login";
        options.LogoutPath = "/tenantadmin/account/logout";
        options.AccessDeniedPath = "/tenantadmin/account/login";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    // Management API: X-API-Key handler …
    .AddScheme<ApiKeyAuthenticationOptions, ApiKeyAuthenticationHandler>(AuthSchemes.ApiKey, _ => { })
    // … and a policy scheme that picks X-API-Key when present, else Bearer (OpenIddict validation).
    .AddPolicyScheme(AuthSchemes.Api, AuthSchemes.Api, options =>
    {
        options.ForwardDefaultSelector = ctx =>
            ctx.Request.Headers.ContainsKey(ApiKeyAuthenticationOptions.HeaderName)
                ? AuthSchemes.ApiKey
                : OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.SuperAdmin, policy => policy
        .AddAuthenticationSchemes(AuthSchemes.SuperAdmin)
        .RequireAuthenticatedUser())
    .AddPolicy(AuthPolicies.User, policy => policy
        .AddAuthenticationSchemes(AuthSchemes.User)
        .RequireAuthenticatedUser())
    .AddPolicy(AuthPolicies.TenantAdmin, policy => policy
        .AddAuthenticationSchemes(AuthSchemes.TenantAdmin)
        .RequireAuthenticatedUser())
    .AddPolicy(AuthPolicies.Api, policy => policy
        .AddAuthenticationSchemes(AuthSchemes.Api)
        .RequireAuthenticatedUser());

// Hangfire: background jobs stored in PostgreSQL, dashboard at /hangfire.
var databaseUrl = builder.Configuration["DATABASE_URL"]
    ?? "Host=localhost;Database=authly;Username=authly;Password=authly";
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(databaseUrl)));
builder.Services.AddHangfireServer();

var app = builder.Build();

// Apply pending EF Core migrations and bootstrap the first super admin on startup.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    sp.GetRequiredService<AppDbContext>().Database.Migrate();

    // Only seed (and later expose) the platform super admin when the surface is enabled.
    if (superAdminEnabled)
        await sp.GetRequiredService<ISuperAdminService>().EnsureSeededAsync(
            app.Configuration["SUPERADMIN_EMAIL"],
            app.Configuration["SUPERADMIN_PASSWORD"]);

    // Phase 13 — record the self-host disclosure acknowledgement at boot (evidence the operator
    // is running a copy that syncs aggregate telemetry; §9 hard rule 4).
    var deployment = sp.GetRequiredService<Authly.Core.Deployment.IDeploymentContext>();
    if (deployment.Mode == Authly.Core.Deployment.DeploymentMode.SelfHosted)
    {
        await sp.GetRequiredService<Authly.Modules.Audit.IAuditLogger>().LogAsync(
            "self_host.disclosure_acknowledged", Authly.Modules.Common.AuditContext.System,
            metadata: new { version = deployment.Version, sync_enabled = deployment.SyncEnabled });
    }
}

// Phase 13 — recurring maintenance + telemetry jobs. Retention runs in every deployment; the
// self-host telemetry push only when self-hosted with a configured sync endpoint/key.
// Use the service-based IRecurringJobManager (resolved from DI) rather than the static RecurringJob
// API, which requires JobStorage.Current and throws at startup under service-based Hangfire setup.
using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    recurringJobs.AddOrUpdate<Authly.Web.Infrastructure.Maintenance.MaintenanceJobs>(
        "retention-expire-transient", j => j.ExpireTransientCredentialsAsync(CancellationToken.None), Cron.Hourly());
    recurringJobs.AddOrUpdate<Authly.Web.Infrastructure.Maintenance.MaintenanceJobs>(
        "retention-purge-history", j => j.PurgeStaleHistoryAsync(CancellationToken.None), Cron.Daily());

    var deployment = scope.ServiceProvider.GetRequiredService<Authly.Core.Deployment.IDeploymentContext>();
    if (deployment.SyncEnabled)
        recurringJobs.AddOrUpdate<Authly.Web.Infrastructure.SelfHost.SelfHostSyncJob>(
            "self-host-telemetry-sync", j => j.PushAsync(CancellationToken.None), "0 */6 * * *");
    else
        recurringJobs.RemoveIfExists("self-host-telemetry-sync");

    // Phase 2 — audit log streaming to an external SIEM/webhook, only when an endpoint is configured.
    if (Authly.Web.Infrastructure.LogStreaming.LogStreamJob.IsConfigured(builder.Configuration))
        recurringJobs.AddOrUpdate<Authly.Web.Infrastructure.LogStreaming.LogStreamJob>(
            "audit-log-stream", j => j.FlushAsync(CancellationToken.None), "*/5 * * * *");
    else
        recurringJobs.RemoveIfExists("audit-log-stream");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Security headers on every response; super-admin IP allowlist + sensitive-endpoint rate limiting.
app.UseMiddleware<Authly.Web.Infrastructure.Security.SecurityHeadersMiddleware>();
app.UseMiddleware<Authly.Web.Infrastructure.Security.SuperAdminIpAllowlistMiddleware>();
app.UseMiddleware<Authly.Web.Infrastructure.Security.RateLimitingMiddleware>();

// When the super-admin surface is disabled (customer-facing builds), hide it entirely: every
// /superadmin route 404s as if it doesn't exist.
if (!superAdminEnabled)
{
    app.Use(async (ctx, next) =>
    {
        if (ctx.Request.Path.StartsWithSegments("/superadmin"))
        {
            ctx.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        await next();
    });
}

app.UseRouting();

// Resolve tenant (sets app.current_tenant for the RLS backstop) before auth.
app.UseMiddleware<TenantResolutionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire dashboard — open in Development, super-admin only otherwise.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter(app.Environment.IsDevelopment()) }
});

app.MapStaticAssets();
app.MapControllerRoute(
        name: "areas",
        pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
