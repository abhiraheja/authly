using Authly.Modules.Applications;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.SuperAdmins;
using Authly.Modules.TenantAdmins;
using Authly.Modules.Tenants;
using Microsoft.Extensions.DependencyInjection;

namespace Authly.Modules;

/// <summary>Registers business-logic services for all modules.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddModules(this IServiceCollection services)
    {
        services.AddScoped<IAuditLogger, AuditLogger>();
        services.AddScoped<ITenantService, TenantService>();
        services.AddScoped<ISuperAdminService, SuperAdminService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IApplicationService, ApplicationService>();
        services.AddScoped<ITenantAdminService, TenantAdminService>();
        return services;
    }
}
