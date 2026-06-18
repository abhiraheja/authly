using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Authly.Core.Interfaces;
using Authly.Infrastructure;
using Authly.Infrastructure.Data;
using Authly.Modules;
using Authly.Modules.Auth;
using Authly.Web.Infrastructure;
using Authly.Web.Infrastructure.Api;
using Authly.Web.Infrastructure.Observability;
using Authly.Web.Infrastructure.Messaging;
using Authly.Web.Infrastructure.OAuth;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor (all panels, hosted login, end-user portal are server-rendered).
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Registered client web-origins (derived from each application's redirect URIs) drive both the
// SPA CORS policy and the CSP form-action directive — a single dynamic source of truth, so adding a
// redirect URI in the admin panel automatically permits that origin for the cross-origin XHRs
// (CORS) and the cross-origin login/logout redirects (CSP). No per-customer config or redeploy.
// CORS_ALLOWED_ORIGINS remains an optional extra for trusted, non-application infrastructure.
var clientStaticOrigins = (builder.Configuration["CORS_ALLOWED_ORIGINS"] ?? string.Empty)
    .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .ToList();
builder.Services.AddMemoryCache();
builder.Services.AddScoped<Authly.Web.Infrastructure.Clients.IClientOriginProvider>(sp =>
    new Authly.Web.Infrastructure.Clients.ClientOriginProvider(
        sp.GetRequiredService<IApplicationRepository>(),
        sp.GetRequiredService<IMemoryCache>(),
        clientStaticOrigins));

// CORS for browser-based (SPA) OAuth/OIDC clients: the discovery, token and userinfo calls are XHRs
// from the SPA origin and need Access-Control-Allow-Origin (the authorize redirect itself is a
// top-level navigation, no CORS). Policy origins come from the client-origin provider above.
builder.Services.AddCors();
builder.Services.AddSingleton<Microsoft.AspNetCore.Cors.Infrastructure.ICorsPolicyProvider,
    Authly.Web.Infrastructure.Cors.ApplicationCorsPolicyProvider>();

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

// Compliance & maintenance (Phase 13): retention/cleanup jobs (run via Hangfire).
builder.Services.AddScoped<Authly.Web.Infrastructure.Maintenance.MaintenanceJobs>();
builder.Services.AddScoped<Authly.Web.Infrastructure.LogStreaming.LogStreamJob>();

// Infrastructure (EF Core, Redis, Argon2id, AES) + business modules.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddModules();

// Pluggable observability (Phase 7): wire OpenTelemetry from the stored config (read at startup).
builder.AddAuthlyObservability();

// Web-layer adapters for module/core abstractions (routing + Hangfire live here).
builder.Services.AddScoped<IAuthUrlBuilder, AuthUrlBuilder>();
builder.Services.AddScoped<IMessageQueue, HangfireMessageQueue>();
builder.Services.AddScoped<MessageDispatchJob>();
builder.Services.AddScoped<Authly.Core.Events.IWebhookQueue, Authly.Web.Infrastructure.Webhooks.HangfireWebhookQueue>();
builder.Services.AddScoped<Authly.Web.Infrastructure.Webhooks.WebhookDispatchJob>();

// OAuth 2.0 / OIDC server (OpenIddict) — endpoints, flows, dev signing keys.
builder.Services.AddAuthlyOpenIddict(builder.Environment.IsDevelopment());
builder.Services.AddScoped<IOAuthClientStore, OpenIddictClientStore>();

// Isolated cookie schemes for tenant end-users and tenant administrators (console operators).
builder.Services.AddAuthentication(AuthSchemes.User)
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

// Apply pending EF Core migrations on startup.
using (var scope = app.Services.CreateScope())
{
    var sp = scope.ServiceProvider;
    sp.GetRequiredService<AppDbContext>().Database.Migrate();
}

// Recurring maintenance jobs (retention/cleanup). Use the service-based IRecurringJobManager
// (resolved from DI) rather than the static RecurringJob API, which requires JobStorage.Current
// and throws at startup under service-based Hangfire setup.
using (var scope = app.Services.CreateScope())
{
    var recurringJobs = scope.ServiceProvider.GetRequiredService<IRecurringJobManager>();

    recurringJobs.AddOrUpdate<Authly.Web.Infrastructure.Maintenance.MaintenanceJobs>(
        "retention-expire-transient", j => j.ExpireTransientCredentialsAsync(CancellationToken.None), Cron.Hourly());
    recurringJobs.AddOrUpdate<Authly.Web.Infrastructure.Maintenance.MaintenanceJobs>(
        "retention-purge-history", j => j.PurgeStaleHistoryAsync(CancellationToken.None), Cron.Daily());

    // Phase 2 — audit log streaming to an external SIEM/webhook. Always scheduled; the job no-ops
    // unless a target is configured (stored observability config, or LOG_STREAM_* env fallback).
    recurringJobs.AddOrUpdate<Authly.Web.Infrastructure.LogStreaming.LogStreamJob>(
        "audit-log-stream", j => j.FlushAsync(CancellationToken.None), "*/5 * * * *");
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();

// Security headers on every response; sensitive-endpoint rate limiting.
app.UseMiddleware<Authly.Web.Infrastructure.Security.SecurityHeadersMiddleware>();
app.UseMiddleware<Authly.Web.Infrastructure.Security.RateLimitingMiddleware>();

app.UseRouting();

// Marketing-website deployment gate: when Website:Enabled is false (the default; docker:
// Website__Enabled), the Home/Docs marketing routes redirect to the admin console, leaving only the
// IdP/console live. Sits after UseRouting (endpoint resolved) and before auth (redirect needs none).
app.UseMiddleware<Authly.Web.Infrastructure.Security.WebsiteGateMiddleware>();

// CORS must sit between routing and auth so preflight (OPTIONS) and the OIDC discovery/token/
// userinfo XHRs from the SPA origin get Access-Control-Allow-Origin headers.
app.UseCors(Authly.Web.Infrastructure.Cors.ApplicationCorsPolicyProvider.SpaPolicyName);

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
