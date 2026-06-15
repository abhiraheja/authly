using System.Buffers.Text;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Modules.AdvancedAuth;
using Authly.Modules.Common;

namespace Authly.Tests.AdvancedAuth;

public class PasskeyServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task BeginRegistration_excludes_existing_credentials()
    {
        var h = new Harness();
        h.AddUser();
        h.AddPasskey(new byte[] { 1, 2, 3 });

        await h.Service.BeginRegistrationAsync(Tenant, UserId);

        var excluded = Assert.Single(h.Gateway.LastExclude);
        Assert.Equal(new byte[] { 1, 2, 3 }, excluded);
    }

    [Fact]
    public async Task CompleteRegistration_stores_an_active_passkey_factor()
    {
        var h = new Harness();
        h.AddUser();
        h.Gateway.NewCredentialId = new byte[] { 7, 7, 7 };

        await h.Service.CompleteRegistrationAsync(Tenant, UserId, "state", "{}", "My Key", AuditContext.System);

        var factor = Assert.Single(h.Factors.Items);
        Assert.Equal(MfaFactorType.Passkey, factor.Type);
        Assert.Equal(MfaFactorStatus.Active, factor.Status);
        Assert.Equal(Base64Url.EncodeToString(new byte[] { 7, 7, 7 }), factor.CredentialId);
        Assert.Equal("My Key", factor.FriendlyName);
        Assert.NotNull(factor.Secret);
        Assert.Contains("user.passkey_registered", h.Audit.Events);
    }

    [Fact]
    public async Task BeginLogin_returns_null_when_the_user_has_no_passkeys()
    {
        var h = new Harness();
        h.AddUser();
        Assert.Null(await h.Service.BeginLoginAsync(Tenant, UserId));
    }

    [Fact]
    public async Task CompleteLogin_verifies_updates_counter_and_returns_the_user()
    {
        var h = new Harness();
        var user = h.AddUser();
        var factor = h.AddPasskey(new byte[] { 5, 5, 5 }, signCount: 3);

        var result = await h.Service.CompleteLoginAsync(Tenant, UserId, "state", "{}");

        Assert.NotNull(result);
        Assert.Equal(user.Id, result!.Id);
        Assert.NotNull(factor.LastUsedAt);                 // marked used
        // The echoed assertion bumped the stored counter from 3 → 4.
        Assert.Contains("\"Sc\":4", factor.Secret);
    }

    [Fact]
    public async Task Remove_ignores_another_users_passkey()
    {
        var h = new Harness();
        h.AddUser();
        var foreign = new MfaFactor
        {
            Id = Guid.NewGuid(), TenantId = Tenant, UserId = Guid.NewGuid(), Type = MfaFactorType.Passkey,
            Status = MfaFactorStatus.Active, CredentialId = "x"
        };
        h.Factors.Items.Add(foreign);

        await h.Service.RemoveAsync(Tenant, UserId, foreign.Id, AuditContext.System);
        Assert.Equal(MfaFactorStatus.Active, foreign.Status); // untouched
    }

    private sealed class Harness
    {
        public readonly FakeMfaFactorRepo Factors = new();
        public readonly FakeUserRepo Users = new();
        public readonly FakeWebAuthnGateway Gateway = new();
        public readonly RecordingAudit Audit = new();
        public readonly PasskeyService Service;

        public Harness() => Service = new PasskeyService(Factors, Users, Gateway, Audit);

        public User AddUser()
        {
            var u = new User { Id = UserId, TenantId = Tenant, Email = "a@example.com", Status = UserStatus.Active };
            Users.Items.Add(u);
            return u;
        }

        public MfaFactor AddPasskey(byte[] credId, uint signCount = 0)
        {
            var secret = System.Text.Json.JsonSerializer.Serialize(new { Pk = Convert.ToBase64String(new byte[] { 1, 2 }), Sc = signCount, Aaguid = Guid.Empty });
            var f = new MfaFactor
            {
                Id = Guid.NewGuid(), TenantId = Tenant, UserId = UserId, Type = MfaFactorType.Passkey,
                Status = MfaFactorStatus.Active, CredentialId = Base64Url.EncodeToString(credId), Secret = secret
            };
            Factors.Items.Add(f);
            return f;
        }
    }
}
