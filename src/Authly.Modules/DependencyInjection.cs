using Authly.Core.Events;
using Authly.Modules.Account;
using Authly.Modules.AdvancedAuth;
using Authly.Modules.ApiKeys;
using Authly.Modules.Applications;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Authorization;
using Authly.Modules.Branding;
using Authly.Modules.Claims;
using Authly.Modules.Hooks;
using Authly.Modules.Messaging;
using Authly.Modules.Mfa;
using Authly.Modules.Security;
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

        // Phase 10 — per-tenant branding + custom domain; end-user portal self-service.
        services.AddScoped<IBrandingService, BrandingService>();
        services.AddScoped<IAccountSelfService, AccountSelfService>();

        // Phase 11 — advanced auth: magic link, secure contact change, recovery, passkeys.
        services.AddScoped<IMagicLinkService, MagicLinkService>();
        services.AddScoped<IContactChangeService, ContactChangeService>();
        services.AddScoped<IRecoveryService, RecoveryService>();
        services.AddScoped<IPasskeyService, PasskeyService>();

        // Phase 12 — security hardening.
        services.AddScoped<ISecuritySettingsService, SecuritySettingsService>();
        services.AddScoped<IBlockListService, BlockListService>();
        services.AddScoped<IAccountLockoutService, AccountLockoutService>();
        services.AddScoped<ISecurityScreeningService, SecurityScreeningService>();
        services.AddScoped<ISuspiciousLoginService, SuspiciousLoginService>();
        services.AddScoped<IConditionalAccessService, ConditionalAccessService>();

        // Phase 13 — self-host telemetry + GDPR/DPDP compliance.
        services.AddScoped<Compliance.IConsentService, Compliance.ConsentService>();
        services.AddScoped<Compliance.IDataRightsService, Compliance.DataRightsService>();
        services.AddScoped<Compliance.ISelfHostSyncService, Compliance.SelfHostSyncService>();

        // Phase 14 — platform ops announcements.
        services.AddScoped<Announcements.IAnnouncementService, Announcements.AnnouncementService>();
        return services;
    }
}
