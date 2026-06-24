using Authly.Core.Entities;
using Authly.Core.Messaging;
using Authly.Core.Enums;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Messaging;
using Authly.Modules.Security;
using Authly.Modules.Social;

namespace Authly.Tests.Security;

public class AuthMethodPolicyTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static AuthMethodPolicy Build(TenantSecuritySettings s, bool whatsAppReady, bool hasSocial)
        => new(new FakeSettings(s), new FakeMessaging(whatsAppReady), new FakeSocial(hasSocial));

    [Fact]
    public void Defaults_enable_all_four_login_methods()
    {
        // A tenant whose stored "security" JSON predates these keys deserializes to the C# defaults.
        var s = new TenantSecuritySettings();
        Assert.True(s.AllowPasswordLogin);
        Assert.True(s.AllowMagicLinkLogin);
        Assert.True(s.AllowPasskeyLogin);
        Assert.True(s.AllowSocialLogin);
    }

    [Fact]
    public async Task Password_magic_passkey_are_effective_iff_their_toggle_is_on()
    {
        var on = await Build(new TenantSecuritySettings(), whatsAppReady: false, hasSocial: false).GetEffectiveAsync(Tenant);
        Assert.True(on.Password);
        Assert.True(on.MagicLink);
        Assert.True(on.Passkey);

        var off = await Build(new TenantSecuritySettings
        {
            AllowPasswordLogin = false, AllowMagicLinkLogin = false, AllowPasskeyLogin = false
        }, whatsAppReady: false, hasSocial: false).GetEffectiveAsync(Tenant);
        Assert.False(off.Password);
        Assert.False(off.MagicLink);
        Assert.False(off.Passkey);
    }

    [Fact]
    public async Task Social_needs_an_active_provider()
    {
        var s = new TenantSecuritySettings { AllowSocialLogin = true };
        Assert.False((await Build(s, whatsAppReady: false, hasSocial: false).GetEffectiveAsync(Tenant)).Social);
        Assert.True((await Build(s, whatsAppReady: false, hasSocial: true).GetEffectiveAsync(Tenant)).Social);

        // Toggle off → not effective even with a provider.
        var sOff = new TenantSecuritySettings { AllowSocialLogin = false };
        Assert.False((await Build(sOff, whatsAppReady: false, hasSocial: true).GetEffectiveAsync(Tenant)).Social);
    }

    [Fact]
    public async Task Phone_needs_whatsapp_otp_ready()
    {
        var s = new TenantSecuritySettings { AllowPhoneLogin = true };
        Assert.False((await Build(s, whatsAppReady: false, hasSocial: false).GetEffectiveAsync(Tenant)).Phone);
        Assert.True((await Build(s, whatsAppReady: true, hasSocial: false).GetEffectiveAsync(Tenant)).Phone);
    }

    [Fact]
    public async Task Any_is_false_only_when_no_method_works()
    {
        var none = new TenantSecuritySettings
        {
            AllowPasswordLogin = false, AllowMagicLinkLogin = false, AllowPasskeyLogin = false,
            AllowSocialLogin = true,  // on, but no provider → ineffective
            AllowPhoneLogin = true    // on, but WhatsApp not ready → ineffective
        };
        var methods = await Build(none, whatsAppReady: false, hasSocial: false).GetEffectiveAsync(Tenant);
        Assert.False(methods.Any);
        Assert.Equal(0, methods.EffectiveCount);

        // Turning the provider on makes social effective → Any.
        var withSocial = await Build(none, whatsAppReady: false, hasSocial: true).GetEffectiveAsync(Tenant);
        Assert.True(withSocial.Any);
    }

    // --- fakes -------------------------------------------------------------

    private sealed class FakeSettings : ISecuritySettingsService
    {
        private readonly TenantSecuritySettings _s;
        public FakeSettings(TenantSecuritySettings s) => _s = s;
        public Task<TenantSecuritySettings> GetAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(_s);
        public Task SaveAsync(Guid tenantId, TenantSecuritySettings settings, string? newCaptchaSecret, AuditContext actor, CancellationToken ct = default) => Task.CompletedTask;
        public string? DecryptCaptchaSecret(TenantSecuritySettings settings) => null;
    }

    private sealed class FakeMessaging : IMessagingService
    {
        private readonly bool _ready;
        public FakeMessaging(bool ready) => _ready = ready;
        public Task<bool> IsWhatsAppOtpReadyAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(_ready);

        // Unused by AuthMethodPolicy.
        public Task DeliverAsync(MessageSendRequest request, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<MessagingProvider>> ListProvidersAsync(Guid tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<MessagingProvider?> GetProviderAsync(Guid tenantId, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveProviderAsync(Guid tenantId, ProviderConfigInput input, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteProviderAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<TemplateListItem>> ListTemplatesAsync(Guid tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<TemplateInput> GetTemplateForEditAsync(Guid tenantId, string key, MessageChannel channel, string locale, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveTemplateAsync(Guid tenantId, TemplateInput input, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteTemplateAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<WhatsAppRemoteTemplate>> SyncWhatsAppTemplatesAsync(Guid tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task BindWhatsAppTemplateAsync(Guid tenantId, string key, string locale, string providerTemplateName, string providerLanguage, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
        public WhatsAppVariableSet? GetWhatsAppVariableSet(string key) => throw new NotImplementedException();
        public Task<IReadOnlyList<WhatsAppSyncedTemplate>> ListSyncableWhatsAppTemplatesAsync(Guid tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task BindWhatsAppTemplateValidatedAsync(Guid tenantId, string key, string locale, string providerTemplateName, string providerLanguage, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<RenderedPreview> PreviewAsync(Guid tenantId, string key, MessageChannel channel, string locale, IReadOnlyDictionary<string, string> sampleVariables, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SendTestAsync(Guid tenantId, string key, MessageChannel channel, string recipient, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<MessageLog>> ListRecentLogsAsync(Guid tenantId, int take = 100, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeSocial : ISocialLoginService
    {
        private readonly bool _has;
        public FakeSocial(bool has) => _has = has;
        public Task<IReadOnlyList<SocialLoginOption>> ListActiveOptionsAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SocialLoginOption>>(_has
                ? new[] { new SocialLoginOption("google", "Google") }
                : System.Array.Empty<SocialLoginOption>());

        // Unused by AuthMethodPolicy.
        public Task<string> BuildAuthorizationUrlAsync(Guid tenantId, string provider, string redirectUri, string state, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SocialLoginResult> CompleteLoginAsync(Guid tenantId, string provider, string code, string redirectUri, RequestInfo info, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<IReadOnlyList<SocialProvider>> ListProvidersAsync(Guid tenantId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<SocialProvider?> GetProviderAsync(Guid tenantId, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task SaveProviderAsync(Guid tenantId, SocialProviderInput input, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
        public Task DeleteProviderAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    }
}
