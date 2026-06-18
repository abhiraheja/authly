using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Infrastructure.Security;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Messaging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Authly.Tests.Messaging;

public class MessagingServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    private static Dictionary<string, string> Vars(params (string, string)[] kv)
        => kv.ToDictionary(x => x.Item1, x => x.Item2);

    [Fact]
    public async Task Delivers_email_through_the_active_provider()
    {
        var h = new Harness();
        await h.SetActiveEmailProviderAsync("testmail");

        await h.Service.DeliverAsync(new MessageSendRequest(Tenant, MessageTemplateKeys.VerifyEmail,
            MessageChannel.Email, "ada@example.com", Vars(("action_url", "https://x/y"))));

        Assert.Equal("ada@example.com", h.Email.LastMessage?.Recipient);
        Assert.Contains("https://x/y", h.Email.LastMessage!.Body);   // rendered link present
        Assert.Equal("sent", h.Log.Entries.Single().Status);
    }

    [Fact]
    public async Task Falls_back_to_log_provider_when_none_configured()
    {
        var h = new Harness();
        await h.Service.DeliverAsync(new MessageSendRequest(Tenant, MessageTemplateKeys.VerifyEmail,
            MessageChannel.Email, "ada@example.com", Vars(("action_url", "https://x/y"))));

        Assert.True(h.LogEmail.Called);                              // the built-in "log" provider handled it
        Assert.Equal("sent", h.Log.Entries.Single().Status);
    }

    [Fact]
    public async Task WhatsApp_failure_falls_back_to_email()
    {
        var h = new Harness();
        await h.SetActiveWhatsAppProviderAsync("testwa", succeeds: false);
        await h.SetActiveEmailProviderAsync("testmail");

        await h.Service.DeliverAsync(new MessageSendRequest(Tenant, MessageTemplateKeys.Otp,
            MessageChannel.WhatsApp, "+15550001111", Vars(("otp", "123456")),
            AllowEmailFallback: true, FallbackEmail: "ada@example.com"));

        Assert.Equal("ada@example.com", h.Email.LastMessage?.Recipient);   // fell back to email
        Assert.Contains("123456", h.Email.LastMessage!.Body);
        Assert.Equal(2, h.Log.Entries.Count);                              // whatsapp failed + email sent
        Assert.Contains(h.Log.Entries, e => e.Channel == MessageChannel.WhatsApp && e.Status == "failed");
        Assert.Contains(h.Log.Entries, e => e.Channel == MessageChannel.Email && e.Status == "sent");
    }

    [Fact]
    public async Task Tenant_override_beats_the_builtin()
    {
        var h = new Harness();
        await h.SetActiveEmailProviderAsync("testmail");
        h.Templates.Items.Add(new MessageTemplate
        {
            Id = Guid.NewGuid(), TenantId = Tenant, Key = MessageTemplateKeys.VerifyEmail,
            Channel = MessageChannel.Email, Locale = "en", Subject = "Custom",
            Body = "CUSTOM {{action_url}}", IsActive = true
        });

        await h.Service.DeliverAsync(new MessageSendRequest(Tenant, MessageTemplateKeys.VerifyEmail,
            MessageChannel.Email, "ada@example.com", Vars(("action_url", "https://x/y"))));

        Assert.StartsWith("CUSTOM", h.Email.LastMessage!.Body);
        Assert.Equal("Custom", h.Email.LastMessage!.Subject);
    }

    [Fact]
    public async Task Saving_a_security_template_without_its_required_variable_is_rejected()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<TemplateValidationException>(() =>
            h.Service.SaveTemplateAsync(Tenant, new TemplateInput
            {
                Key = MessageTemplateKeys.VerifyEmail, Channel = MessageChannel.Email, Locale = "en",
                Subject = "Hi", Body = "No link here", IsActive = true
            }, AuditContext.System));
    }

    [Fact]
    public async Task Provider_secrets_are_encrypted_at_rest()
    {
        var h = new Harness();
        await h.Service.SaveProviderAsync(Tenant, new ProviderConfigInput
        {
            Channel = MessageChannel.Email, Provider = "zepto", SenderEmail = "no-reply@x.com",
            ApiKey = "super-secret-key", IsActive = true
        }, AuditContext.System);

        var stored = h.Providers.Items.Single();
        Assert.DoesNotContain("super-secret-key", stored.Config);    // not stored in the clear
    }

    [Fact]
    public async Task Preview_renders_sample_variables()
    {
        var h = new Harness();
        var preview = await h.Service.PreviewAsync(Tenant, MessageTemplateKeys.Otp, MessageChannel.Email, "en",
            Vars(("otp", "987654")));
        Assert.Contains("987654", preview.Body);
    }

    [Fact]
    public async Task WhatsApp_send_is_free_text_when_no_template_bound()
    {
        var h = new Harness();
        await h.SetActiveWhatsAppProviderAsync("testwa", succeeds: true);

        await h.Service.DeliverAsync(new MessageSendRequest(Tenant, MessageTemplateKeys.Otp,
            MessageChannel.WhatsApp, "+15550001111", Vars(("otp", "445566"), ("expiry_minutes", "10"))));

        Assert.NotNull(h.WhatsApp.LastMessage);
        Assert.Null(h.WhatsApp.LastMessage!.WhatsAppTemplateName);
        Assert.Contains("445566", h.WhatsApp.LastMessage.Body);
    }

    [Fact]
    public async Task Bind_then_send_uses_template_mode_with_positional_params()
    {
        var h = new Harness();
        await h.SetActiveWhatsAppProviderAsync("testwa", succeeds: true);

        await h.Service.BindWhatsAppTemplateAsync(Tenant, MessageTemplateKeys.Otp, "en",
            "authly_otp", "en_US", AuditContext.System);

        await h.Service.DeliverAsync(new MessageSendRequest(Tenant, MessageTemplateKeys.Otp,
            MessageChannel.WhatsApp, "+15550001111", Vars(("otp", "778899"), ("expiry_minutes", "10"))));

        var msg = h.WhatsApp.LastMessage!;
        Assert.Equal("authly_otp", msg.WhatsAppTemplateName);
        Assert.Equal("en_US", msg.WhatsAppLanguage);
        Assert.NotNull(msg.WhatsAppParameters);
        Assert.Equal("778899", msg.WhatsAppParameters![0]); // {{1}} = otp, per the spec
    }

    [Fact]
    public async Task Sync_returns_provider_templates()
    {
        var h = new Harness();
        await h.SetActiveWhatsAppProviderAsync("testwa", succeeds: true);
        h.Directory.Templates.Add(new WhatsAppRemoteTemplate("authly_otp", "en_US", "APPROVED", "AUTHENTICATION", "{{1}} is your code.", 1));

        var result = await h.Service.SyncWhatsAppTemplatesAsync(Tenant);

        Assert.Single(result);
        Assert.Equal("authly_otp", result[0].Name);
    }

    [Fact]
    public async Task Sync_without_active_provider_throws()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<InvalidOperationException>(() => h.Service.SyncWhatsAppTemplatesAsync(Tenant));
    }

    // --- harness ------------------------------------------------------------

    private sealed class Harness
    {
        public readonly FakeProviderRepo Providers = new();
        public readonly FakeTemplateRepo Templates = new();
        public readonly FakeLogRepo Log = new();
        public readonly RecordingEmailProvider Email = new("testmail");
        public readonly RecordingEmailProvider LogEmail = new("log");
        public readonly RecordingWhatsAppProvider WhatsApp = new("testwa", succeeds: true);
        public readonly RecordingWhatsAppProvider LogWhatsApp = new("log", succeeds: true);
        public readonly RecordingWhatsAppDirectory Directory = new("testwa");
        public readonly AesEncryptionService Encryption =
            new(Options.Create(new EncryptionOptions { Key = "3J8mZ1qg9X0vQpYb2sR7tU4wK6nL5cD8eF1aH0iJ2kM=" }));
        public readonly MessagingService Service;

        public Harness()
        {
            Service = new MessagingService(Providers, Templates, Log,
                new IEmailProvider[] { Email, LogEmail },
                new IWhatsAppProvider[] { WhatsApp, LogWhatsApp },
                new IWhatsAppTemplateDirectory[] { Directory },
                Encryption, new RecordingAuditLogger(), NullLogger<MessagingService>.Instance);
        }

        public Task SetActiveEmailProviderAsync(string name)
        {
            Providers.Items.Add(new MessagingProvider
            {
                Id = Guid.NewGuid(), TenantId = Tenant, Channel = MessageChannel.Email,
                Provider = name, Mode = "byok", Config = "{}", IsActive = true, CreatedAt = DateTimeOffset.UtcNow
            });
            return Task.CompletedTask;
        }

        public Task SetActiveWhatsAppProviderAsync(string name, bool succeeds)
        {
            WhatsApp.Succeeds = succeeds;
            Providers.Items.Add(new MessagingProvider
            {
                Id = Guid.NewGuid(), TenantId = Tenant, Channel = MessageChannel.WhatsApp,
                Provider = name, Mode = "byok", Config = "{}", IsActive = true, CreatedAt = DateTimeOffset.UtcNow
            });
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEmailProvider : IEmailProvider
    {
        public RecordingEmailProvider(string name) => Name = name;
        public string Name { get; }
        public bool Called { get; private set; }
        public RenderedMessage? LastMessage { get; private set; }
        public Task<DeliveryResult> SendAsync(RenderedMessage message, EmailProviderConfig config, CancellationToken ct = default)
        {
            Called = true; LastMessage = message;
            return Task.FromResult(DeliveryResult.Ok(Name));
        }
    }

    private sealed class RecordingWhatsAppProvider : IWhatsAppProvider
    {
        public RecordingWhatsAppProvider(string name, bool succeeds) { Name = name; Succeeds = succeeds; }
        public string Name { get; }
        public bool Succeeds { get; set; }
        public RenderedMessage? LastMessage { get; private set; }
        public Task<DeliveryResult> SendAsync(RenderedMessage message, WhatsAppProviderConfig config, CancellationToken ct = default)
        {
            LastMessage = message;
            return Task.FromResult(Succeeds ? DeliveryResult.Ok(Name) : DeliveryResult.Fail(Name, "boom"));
        }
    }

    private sealed class RecordingWhatsAppDirectory : IWhatsAppTemplateDirectory
    {
        public RecordingWhatsAppDirectory(string name) => Name = name;
        public string Name { get; }
        public List<WhatsAppRemoteTemplate> Templates { get; } = new();
        public Task<IReadOnlyList<WhatsAppRemoteTemplate>> ListAsync(WhatsAppProviderConfig config, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<WhatsAppRemoteTemplate>>(Templates);
    }

    private sealed class FakeProviderRepo : IMessagingProviderRepository
    {
        public readonly List<MessagingProvider> Items = new();
        public Task<IReadOnlyList<MessagingProvider>> ListByTenantAsync(Guid t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MessagingProvider>>(Items.Where(p => p.TenantId == t).ToList());
        public Task<MessagingProvider?> GetActiveAsync(Guid t, MessageChannel ch, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Channel == ch && p.IsActive));
        public Task<MessagingProvider?> GetByIdAsync(Guid t, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id));
        public Task AddAsync(MessagingProvider p, CancellationToken ct = default) { if (p.Id == Guid.Empty) p.Id = Guid.NewGuid(); Items.Add(p); return Task.CompletedTask; }
        public Task UpdateAsync(MessagingProvider p, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(MessagingProvider p, CancellationToken ct = default) { Items.Remove(p); return Task.CompletedTask; }
    }

    private sealed class FakeTemplateRepo : IMessageTemplateRepository
    {
        public readonly List<MessageTemplate> Items = new();
        public Task<IReadOnlyList<MessageTemplate>> ListByTenantAsync(Guid t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MessageTemplate>>(Items.Where(x => x.TenantId == t).ToList());
        public Task<MessageTemplate?> GetAsync(Guid t, string key, MessageChannel ch, string locale, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Key == key && x.Channel == ch && x.Locale == locale && x.IsActive));
        public Task<MessageTemplate?> GetByIdAsync(Guid t, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(x => x.TenantId == t && x.Id == id));
        public Task AddAsync(MessageTemplate x, CancellationToken ct = default) { if (x.Id == Guid.Empty) x.Id = Guid.NewGuid(); Items.Add(x); return Task.CompletedTask; }
        public Task UpdateAsync(MessageTemplate x, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(MessageTemplate x, CancellationToken ct = default) { Items.Remove(x); return Task.CompletedTask; }
    }

    private sealed class FakeLogRepo : IMessageLogRepository
    {
        public readonly List<MessageLog> Entries = new();
        public Task AddAsync(MessageLog entry, CancellationToken ct = default) { Entries.Add(entry); return Task.CompletedTask; }
        public Task<IReadOnlyList<MessageLog>> ListRecentByTenantAsync(Guid t, int take, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MessageLog>>(Entries.Where(e => e.TenantId == t).Take(take).ToList());
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null,
            string? resourceType = null, Guid? resourceId = null, string result = "success",
            object? metadata = null, CancellationToken ct = default) => Task.CompletedTask;
    }
}
