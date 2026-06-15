using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Authly.Infrastructure;
using Authly.Infrastructure.Data;
using Authly.Modules;
using Authly.Modules.SuperAdmins;
using Authly.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor (all panels, hosted login, end-user portal are server-rendered).
builder.Services.AddControllersWithViews();

// Infrastructure (EF Core, Redis, Argon2id, AES) + business modules.
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddModules();

// Super-admin authentication — an isolated cookie scheme, separate from tenant users.
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
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.SuperAdmin, policy => policy
        .AddAuthenticationSchemes(AuthSchemes.SuperAdmin)
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
