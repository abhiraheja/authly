using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Infrastructure.Security;
using Authly.Modules.AdvancedAuth;
using Authly.Modules.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authly.Tests.AdvancedAuth;

public class MagicLinkServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Request_issues_a_token_and_queues_a_magic_link_email()
    {
        var h = new Harness();
        h.AddUser("a@example.com");

        await h.Service.RequestAsync(Tenant, "A@Example.com", RequestInfo.Unknown);

        var token = Assert.Single(h.Tokens.Items);
        Assert.Equal("magic_link", token.Type);
        var msg = Assert.Single(h.Queue.Items);
        Assert.Equal("magic_link", msg.TemplateKey);
        Assert.Contains("user.magic_link_requested", h.Audit.Events);
    }

    [Fact]
    public async Task Request_is_silent_for_an_unknown_email()
    {
        var h = new Harness();
        await h.Service.RequestAsync(Tenant, "ghost@example.com", RequestInfo.Unknown);
        Assert.Empty(h.Tokens.Items);
        Assert.Empty(h.Queue.Items);
    }

    [Fact]
    public async Task Complete_signs_in_once_then_rejects_reuse_and_verifies_email()
    {
        var h = new Harness();
        var user = h.AddUser("a@example.com", emailVerified: false);
        await h.Service.RequestAsync(Tenant, "a@example.com", RequestInfo.Unknown);
        var raw = h.Urls.LastMagic!;

        var first = await h.Service.CompleteAsync(Tenant, raw);
        Assert.NotNull(first);
        Assert.Equal(user.Id, first!.Id);
        Assert.True(user.EmailVerified);                 // opening the link verifies the inbox

        Assert.Null(await h.Service.CompleteAsync(Tenant, raw)); // single-use
    }

    [Fact]
    public async Task Complete_rejects_an_expired_token()
    {
        var h = new Harness();
        h.AddUser("a@example.com");
        await h.Service.RequestAsync(Tenant, "a@example.com", RequestInfo.Unknown);
        h.Tokens.Items.Single().ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);

        Assert.Null(await h.Service.CompleteAsync(Tenant, h.Urls.LastMagic!));
    }

    private sealed class Harness
    {
        public readonly FakeUserRepo Users = new();
        public readonly FakeVerificationRepo Tokens = new();
        public readonly CapturingQueue Queue = new();
        public readonly CapturingUrlBuilder Urls = new();
        public readonly RecordingAudit Audit = new();
        public readonly MagicLinkService Service;

        public Harness() => Service = new MagicLinkService(Users, Tokens, new Sha256TokenHasher(),
            Queue, Urls, Audit, NullLogger<MagicLinkService>.Instance);

        public User AddUser(string email, bool emailVerified = true)
        {
            var u = new User { Id = Guid.NewGuid(), TenantId = Tenant, Email = email, EmailVerified = emailVerified, Status = UserStatus.Active };
            Users.Items.Add(u);
            return u;
        }
    }
}
