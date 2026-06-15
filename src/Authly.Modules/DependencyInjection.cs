using Authly.Core.Events;
using Authly.Modules.ApiKeys;
using Authly.Modules.Applications;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Authorization;
using Authly.Modules.Claims;
using Authly.Modules.Hooks;
using Authly.Modules.Messaging;
using Authly.Modules.Mfa;
using Authly.Modules.Social;
using Authly.Modules.Users;
using Authly.Modules.SuperAdmins;
using Authly.Modules.TenantAdmins;
using Authly.Modules.Tenants;
using Authly.Modules.Webhooks;
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
        services.AddScoped<IRbacService, RbacService>();
        services.AddScoped<IApiKeyService, ApiKeyService>();
        services.AddScoped<IUserAdminService, UserAdminService>();
        services.AddScoped<IMfaService, MfaService>();
        services.AddScoped<IMessagingService, MessagingService>();
        services.AddScoped<ISocialLoginService, SocialLoginService>();

        // Phase 9 — webhooks, pipeline hooks, custom token claims.
        services.AddScoped<IEventPublisher, EventPublisher>();
        services.AddScoped<IWebhookService, WebhookService>();
        services.AddScoped<IPipelineHookService, PipelineHookService>();
        services.AddScoped<IClaimConfigService, ClaimConfigService>();
        services.AddScoped<ITokenClaimAssembler, TokenClaimAssembler>();
        return services;
    }
}
