using Hangfire;
using Hangfire.PostgreSql;
using Saarvix.Identity.Infrastructure;
using Saarvix.Identity.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// MVC + Razor (all panels, hosted login, end-user portal are server-rendered).
builder.Services.AddControllersWithViews();

// Infrastructure: EF Core (PostgreSQL), Redis, Argon2id hasher, AES encryption.
builder.Services.AddInfrastructure(builder.Configuration);

// Hangfire: background jobs stored in PostgreSQL, dashboard at /hangfire.
var databaseUrl = builder.Configuration["DATABASE_URL"]
    ?? "Host=localhost;Database=saarvix;Username=saarvix;Password=saarvix";
builder.Services.AddHangfire(cfg => cfg
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(o => o.UseNpgsqlConnection(databaseUrl)));
builder.Services.AddHangfireServer();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

// Hangfire dashboard. Open in Development; locked to super admins in Phase 1.
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardAuthorizationFilter(app.Environment.IsDevelopment()) }
});

app.MapStaticAssets();
app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
