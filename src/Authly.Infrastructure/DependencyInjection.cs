using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Authly.Core.Events;
using Authly.Core.Interfaces;
using Authly.Core.OAuth;
using Authly.Infrastructure.Data;
using Authly.Infrastructure.Data.Repositories;
using Authly.Infrastructure.Events;
using Authly.Infrastructure.Messaging;
using Authly.Infrastructure.OAuth;
using Authly.Infrastructure.Security;
using Authly.Infrastructure.Tenancy;
using StackExchange.Redis;

namespace Authly.Infrastructure;

/// <summary>Composition-root registration for the Infrastructure layer.</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        // --- PostgreSQL / EF Core ---
        var databaseUrl = config["DATABASE_URL"]
            ?? "Host=localhost;Database=authly;Username=authly;Password=authly";
        services.AddScoped<TenantConnectionInterceptor>();
        services.AddDbContext<AppDbContext>((sp, opt) =>
        {
            opt.UseNpgsql(databaseUrl)
               .AddInterceptors(sp.GetRequiredService<TenantConnectionInterceptor>());
            opt.UseOpenIddict(); // register OpenIddict's EF Core entity stores
        });

        // --- Redis (cache, sessions, rate limits) ---
        var redisUrl = config["REDIS_URL"] ?? "localhost:6379";
        services.AddSingleton<IConnectionMultiplexer>(_ =>
            ConnectionMultiplexer.Connect(redisUrl));
        services.AddStackExchangeRedisCache(o => o.Configuration = redisUrl);

        // --- Security primitives ---
        services.Configure<EncryptionOptions>(o => o.Key = config["ENCRYPTION_KEY"]!);
        services.AddSingleton<IPasswordHasher, Argon2idPasswordHasher>();
        services.AddSingleton<IEncryptionService, AesEncryptionService>();
        services.AddSingleton<ITokenHasher, Sha256TokenHasher>();
        services.AddSingleton<ICredentialGenerator, CredentialGenerator>();
        services.AddSingleton<ITotpService, TotpService>();

        // --- Messaging (Phase 7: pluggable BYOK providers selected per tenant) ---
        services.AddHttpClient();
        services.AddScoped<IEmailProvider, LogEmailProvider>();
        services.AddScoped<IEmailProvider, SmtpEmailProvider>();
        services.AddScoped<IEmailProvider, ZeptoEmailProvider>();
        services.AddScoped<IWhatsAppProvider, LogWhatsAppProvider>();
        services.AddScoped<IWhatsAppProvider, Msg91WhatsAppProvider>();

        // --- Social / OAuth2 login gateway (HTTP) ---
        services.AddScoped<ISocialAuthGateway, SocialAuthGateway>();

        // --- WebAuthn / FIDO2 passkey ceremony (Phase 11) ---
        services.AddScoped<Authly.Core.WebAuthn.IWebAuthnGateway, Fido2WebAuthnGateway>();

        // --- Security hardening (Phase 12): rate limiting, lockout, breached-pw, CAPTCHA ---
        services.AddSingleton<Authly.Core.Security.IRateLimiter, RedisRateLimiter>();
        services.AddSingleton<Authly.Core.Security.ILoginAttemptStore, RedisLoginAttemptStore>();
        services.AddScoped<Authly.Core.Security.IPwnedRangeClient, PwnedRangeClient>();
        services.AddScoped<Authly.Core.Security.IBreachedPasswordGateway, HibpBreachedPasswordGateway>();
        services.AddScoped<Authly.Core.Security.ICaptchaGateway, HttpCaptchaGateway>();

        // --- Webhooks & pipeline hooks (HTTP transports) ---
        services.AddScoped<IWebhookSender, HttpWebhookSender>();
        services.AddScoped<IPipelineHookClient, HttpPipelineHookClient>();

        // --- Deployment / self-host (Phase 13): mode + version + sync config from env (§10). ---
        services.AddSingleton<Authly.Core.Deployment.IDeploymentContext, Deployment.DeploymentContext>();

        // --- Compliance & retention (Phase 13): GDPR/DPDP export+erasure, telemetry, cleanup. ---
        services.AddScoped<Authly.Core.Compliance.IComplianceDataStore, Compliance.ComplianceDataStore>();
        services.AddScoped<Authly.Core.Compliance.IInstanceMetricsCollector, Compliance.InstanceMetricsCollector>();
        services.AddScoped<Authly.Core.Compliance.IRetentionStore, Compliance.RetentionStore>();

        // --- Monitoring (Phase 14): platform health probe + cross-tenant login analytics. ---
        services.AddScoped<Authly.Core.Monitoring.IPlatformHealthProbe, Monitoring.PlatformHealthProbe>();
        services.AddScoped<Authly.Core.Monitoring.ILoginAnalyticsStore, Monitoring.LoginAnalyticsStore>();

        // --- Tenancy ---
        services.AddScoped<ITenantContext, TenantContext>();

        // --- Repositories ---
        services.AddScoped<ITenantRepository, TenantRepository>();
        services.AddScoped<ISuperAdminRepository, SuperAdminRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<ILoginHistoryRepository, LoginHistoryRepository>();
        services.AddScoped<IVerificationTokenRepository, VerificationTokenRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUserRoleRepository, UserRoleRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IMfaFactorRepository, MfaFactorRepository>();
        services.AddScoped<IMfaBackupCodeRepository, MfaBackupCodeRepository>();
        services.AddScoped<IOtpCodeRepository, OtpCodeRepository>();
        services.AddScoped<IMessagingProviderRepository, MessagingProviderRepository>();
        services.AddScoped<IMessageTemplateRepository, MessageTemplateRepository>();
        services.AddScoped<IMessageLogRepository, MessageLogRepository>();
        services.AddScoped<ISocialIdentityRepository, SocialIdentityRepository>();
        services.AddScoped<ISocialProviderRepository, SocialProviderRepository>();
        services.AddScoped<IWebhookEndpointRepository, WebhookEndpointRepository>();
        services.AddScoped<IWebhookDeliveryRepository, WebhookDeliveryRepository>();
        services.AddScoped<IPipelineHookRepository, PipelineHookRepository>();
        services.AddScoped<IClaimConfigRepository, ClaimConfigRepository>();
        services.AddScoped<IRecoveryContactRepository, RecoveryContactRepository>();
        services.AddScoped<IPendingContactChangeRepository, PendingContactChangeRepository>();
        services.AddScoped<IConsentRecordRepository, ConsentRecordRepository>();
        services.AddScoped<ISelfHostedInstanceRepository, SelfHostedInstanceRepository>();
        services.AddScoped<IAnnouncementRepository, AnnouncementRepository>();
        services.AddScoped<IUserDeviceRepository, UserDeviceRepository>();
        services.AddScoped<IPlatformStateStore, PlatformStateStore>();
        services.AddScoped<Authly.Core.Logging.IAuditLogStreamSource, Logging.AuditLogStreamSource>();

        return services;
    }
}
