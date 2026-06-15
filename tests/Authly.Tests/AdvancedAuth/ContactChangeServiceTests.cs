using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Infrastructure.Security;
using Authly.Modules.AdvancedAuth;
using Authly.Modules.Common;

namespace Authly.Tests.AdvancedAuth;

public class ContactChangeServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Request_creates_pending_and_notifies_new_and_old_contacts()
    {
        var h = new Harness();
        var user = h.AddUser("old@example.com");

        var outcome = await h.Service.RequestChangeAsync(Tenant, user.Id, ContactType.Email, "New@Example.com", RequestInfo.Unknown);

        Assert.Equal(ContactChangeOutcome.Started, outcome);
        var pending = Assert.Single(h.Changes.Items);
        Assert.Equal("new@example.com", pending.NewValue);                 // normalized
        Assert.Equal(2, h.Queue.Items.Count);                              // verify-to-new + alert-to-old
        Assert.Contains(h.Queue.Items, m => m.TemplateKey == "verify_new_contact" && m.Recipient == "new@example.com");
        Assert.Contains(h.Queue.Items, m => m.TemplateKey == "contact_change_alert" && m.Recipient == "old@example.com");
        Assert.Contains("user.contact_change_requested", h.Audit.Events);
    }

    [Fact]
    public async Task Request_rejects_an_email_already_in_use()
    {
        var h = new Harness();
        var user = h.AddUser("old@example.com");
        h.AddUser("taken@example.com");

        var outcome = await h.Service.RequestChangeAsync(Tenant, user.Id, ContactType.Email, "taken@example.com", RequestInfo.Unknown);
        Assert.Equal(ContactChangeOutcome.AlreadyInUse, outcome);
    }

    [Fact]
    public async Task Request_is_rate_limited_by_cooldown()
    {
        var h = new Harness();
        var user = h.AddUser("old@example.com");

        await h.Service.RequestChangeAsync(Tenant, user.Id, ContactType.Email, "first@example.com", RequestInfo.Unknown);
        var second = await h.Service.RequestChangeAsync(Tenant, user.Id, ContactType.Email, "second@example.com", RequestInfo.Unknown);

        Assert.Equal(ContactChangeOutcome.Cooldown, second);
    }

    [Fact]
    public async Task Verify_applies_the_new_email_once()
    {
        var h = new Harness();
        var user = h.AddUser("old@example.com");
        await h.Service.RequestChangeAsync(Tenant, user.Id, ContactType.Email, "new@example.com", RequestInfo.Unknown);
        var raw = h.Urls.LastVerify!;

        Assert.True(await h.Service.VerifyAsync(Tenant, raw));
        Assert.Equal("new@example.com", user.Email);
        Assert.True(user.EmailVerified);
        Assert.Equal(ContactChangeStatus.Completed, h.Changes.Items.Single().Status);
        Assert.Contains("user.email_changed", h.Audit.Events);

        Assert.False(await h.Service.VerifyAsync(Tenant, raw)); // not pending anymore
    }

    [Fact]
    public async Task Cancel_from_the_old_address_aborts_the_change()
    {
        var h = new Harness();
        var user = h.AddUser("old@example.com");
        await h.Service.RequestChangeAsync(Tenant, user.Id, ContactType.Email, "new@example.com", RequestInfo.Unknown);
        var cancel = h.Urls.LastCancel!;

        Assert.True(await h.Service.CancelAsync(cancel));
        Assert.Equal(ContactChangeStatus.Cancelled, h.Changes.Items.Single().Status);
        Assert.Equal("old@example.com", user.Email);          // unchanged
        Assert.Contains("user.contact_change_cancelled", h.Audit.Events);

        // After cancellation, the verify link no longer applies the change.
        Assert.False(await h.Service.VerifyAsync(Tenant, h.Urls.LastVerify!));
    }

    private sealed class Harness
    {
        public readonly FakeUserRepo Users = new();
        public readonly FakePendingChangeRepo Changes = new();
        public readonly CapturingQueue Queue = new();
        public readonly CapturingUrlBuilder Urls = new();
        public readonly RecordingAudit Audit = new();
        public readonly ContactChangeService Service;

        public Harness() => Service = new ContactChangeService(Users, Changes, new Sha256TokenHasher(), Queue, Urls, Audit);

        public User AddUser(string email)
        {
            var u = new User { Id = Guid.NewGuid(), TenantId = Tenant, Email = email, EmailVerified = true, Status = UserStatus.Active };
            Users.Items.Add(u);
            return u;
        }
    }
}
