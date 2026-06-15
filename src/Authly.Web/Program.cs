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
using Authly.Web.Infrastructure.Messaging;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor (all panels, hosted login, end-user portal are server-rendered).
builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

// Infrastructure (EF Core, Redis, Argon2id, AES) + business modules.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddModules();

// Web-layer adapters for module/core abstractions (routing + Hangfire live here).
builder.Services.AddScoped<IAuthUrlBuilder, AuthUrlBuilder>();
builder.Services.AddScoped<IEmailQueue, HangfireEmailQueue>();
builder.Services.AddScoped<EmailDispatchJob>();

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
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.SuperAdmin, policy => policy
        .AddAuthenticationSchemes(AuthSchemes.SuperAdmin)
        .RequireAuthenticatedUser())
    .AddPolicy(AuthPolicies.User, policy => policy
        .AddAuthenticationSchemes(AuthSchemes.User)
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
