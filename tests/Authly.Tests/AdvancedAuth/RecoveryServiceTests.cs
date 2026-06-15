using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Infrastructure.Security;
using Authly.Modules.AdvancedAuth;
using Authly.Modules.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authly.Tests.AdvancedAuth;

public class RecoveryServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task AddContact_persists_and_audits_and_is_idempotent()
    {
        var h = new Harness();
        await h.Service.AddContactAsync(Tenant, UserId, ContactType.Email, "Backup@Example.com", AuditContext.System);
        await h.Service.AddContactAsync(Tenant, UserId, ContactType.Email, "backup@example.com", AuditContext.System); // dup

        Assert.Single(h.Contacts.Items);
        Assert.Equal("backup@example.com", h.Contacts.Items[0].Value);
        Assert.Contains("user.recovery_contact_added", h.Audit.Events);
    }

    [Fact]
    public async Task RemoveContact_ignores_another_users_contact()
    {
        var h = new Harness();
        var foreign = new RecoveryContact { Id = Guid.NewGuid(), TenantId = Tenant, UserId = Guid.NewGuid(), Type = ContactType.Email, Value = "x@y.com" };
        h.Contacts.Items.Add(foreign);

        await h.Service.RemoveContactAsync(Tenant, UserId, foreign.Id, AuditContext.System);
        Assert.Contains(foreign, h.Contacts.Items); // untouched
    }

    [Fact]
    public async Task Initiate_notifies_the_primary_email_plus_every_contact_and_audits()
    {
        var h = new Harness();
        h.Users.Items.Add(new User { Id = UserId, TenantId = Tenant, Email = "user@example.com", Status = UserStatus.Active });
        h.Contacts.Items.Add(new RecoveryContact { Id = Guid.NewGuid(), TenantId = Tenant, UserId = UserId, Type = ContactType.Email, Value = "backup@example.com" });
        h.Contacts.Items.Add(new RecoveryContact { Id = Guid.NewGuid(), TenantId = Tenant, UserId = UserId, Type = ContactType.Phone, Value = "+15551234567" });

        await h.Service.InitiateRecoveryAsync(Tenant, "user@example.com", RequestInfo.Unknown);

        Assert.Single(h.Resets.Items);                              // one recovery token
        Assert.Equal(3, h.Queue.Items.Count);                       // primary email + 2 contacts
        Assert.All(h.Queue.Items, m => Assert.Equal("account_recovery", m.TemplateKey));
        Assert.Contains("user.recovery_initiated", h.Audit.Events);
    }

    [Fact]
    public async Task Initiate_is_silent_for_an_unknown_email()
    {
        var h = new Harness();
        await h.Service.InitiateRecoveryAsync(Tenant, "ghost@example.com", RequestInfo.Unknown);
        Assert.Empty(h.Resets.Items);
        Assert.Empty(h.Queue.Items);
    }

    private sealed class Harness
    {
        public readonly FakeRecoveryContactRepo Contacts = new();
        public readonly FakeUserRepo Users = new();
        public readonly FakeResetRepo Resets = new();
        public readonly CapturingQueue Queue = new();
        public readonly CapturingUrlBuilder Urls = new();
        public readonly RecordingAudit Audit = new();
        public readonly RecoveryService Service;

        public Harness() => Service = new RecoveryService(Contacts, Users, Resets, new Sha256TokenHasher(),
            Queue, Urls, Audit, NullLogger<RecoveryService>.Instance);
    }
}
