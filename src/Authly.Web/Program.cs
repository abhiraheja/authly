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

    await sp.GetRequiredService<ISuperAdminService>().EnsureSeededAsync(
        app.Configuration["SUPERADMIN_EMAIL"],
        app.Configuration["SUPERADMIN_PASSWORD"]);
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
