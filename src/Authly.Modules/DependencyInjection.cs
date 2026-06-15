using Authly.Modules.Audit;
using Authly.Modules.SuperAdmins;
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
        return services;
    }
}
