using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Account;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Tests.Account;

public class AccountSelfServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid UserId = Guid.NewGuid();

    [Fact]
    public async Task ChangePassword_rejects_a_wrong_current_password()
    {
        var h = new Harness();
        h.User.PasswordHash = h.Hasher.Hash("correct-horse");

        var result = await h.Service.ChangePasswordAsync(Tenant, UserId, "wrong", "new-password", Guid.NewGuid(), AuditContext.System);

        Assert.Equal(PasswordChangeResult.WrongCurrentPassword, result);
        Assert.True(h.Hasher.Verify(h.User.PasswordHash!, "correct-horse")); // unchanged
    }

    [Fact]
    public async Task ChangePassword_succeeds_and_revokes_other_sessions_keeping_current()
    {
        var h = new Harness();
        h.User.PasswordHash = h.Hasher.Hash("correct-horse");
        var current = h.AddActiveSession();
        var other = h.AddActiveSession();

        var result = await h.Service.ChangePasswordAsync(Tenant, UserId, "correct-horse", "new-password", current, AuditContext.System);

        Assert.Equal(PasswordChangeResult.Success, result);
        Assert.True(h.Hasher.Verify(h.User.PasswordHash!, "new-password"));
        Assert.False(h.Sessions.Store[current].Revoked); // current kept
        Assert.True(h.Sessions.Store[other].Revoked);     // others evicted
        Assert.Contains("user.password_changed", h.Audit.Events);
    }

    [Fact]
    public async Task ChangePassword_allows_a_social_only_account_to_set_a_first_password()
    {
        var h = new Harness();
        h.User.PasswordHash = null; // social-only

        var result = await h.Service.ChangePasswordAsync(Tenant, UserId, null, "brand-new", h.CurrentSession, AuditContext.System);

        Assert.Equal(PasswordChangeResult.Success, result);
        Assert.NotNull(h.User.PasswordHash);
        Assert.True(h.Hasher.Verify(h.User.PasswordHash!, "brand-new"));
    }

    [Fact]
    public async Task RevokeSession_ignores_a_session_belonging_to_another_user()
    {
        var h = new Harness();
        var foreign = new Session
        {
            Id = Guid.NewGuid(), TenantId = Tenant, UserId = Guid.NewGuid(), RefreshTokenHash = "x",
            ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        };
        h.Sessions.Store[foreign.Id] = foreign;

        await h.Service.RevokeSessionAsync(Tenant, UserId, foreign.Id, AuditContext.System);

        Assert.False(foreign.Revoked); // untouched — not this user's session
        Assert.DoesNotContain("session.revoked", h.Audit.Events);
    }

    [Fact]
    public async Task RevokeSession_revokes_the_users_own_session_and_audits()
    {
        var h = new Harness();
        var own = h.AddActiveSession();

        await h.Service.RevokeSessionAsync(Tenant, UserId, own, AuditContext.System);

        Assert.True(h.Sessions.Store[own].Revoked);
        Assert.Contains("session.revoked", h.Audit.Events);
    }

    [Fact]
    public async Task UpdateProfile_normalizes_and_persists()
    {
        var h = new Harness();
        var ok = await h.Service.UpdateProfileAsync(Tenant, UserId,
            new ProfileUpdate("  Ada  ", "  Lovelace  ", "", ""), AuditContext.System);

        Assert.True(ok);
        Assert.Equal("Ada", h.User.FirstName);
        Assert.Equal("Lovelace", h.User.LastName);
        Assert.Equal("UTC", h.User.Timezone);  // blank → default
        Assert.Equal("en", h.User.Locale);
        Assert.Contains("user.profile_updated", h.Audit.Events);
    }

    // --- harness ------------------------------------------------------------

    private sealed class Harness
    {
        public readonly FakeUserRepo Users = new();
        public readonly FakeSessionRepo Sessions = new();
        public readonly FakeLoginHistoryRepo History = new();
        public readonly FakeHasher Hasher = new();
        public readonly RecordingAudit Audit = new();
        public readonly AccountSelfService Service;
        public readonly User User;
        public readonly Guid CurrentSession = Guid.NewGuid();

        public Harness()
        {
            User = new User { Id = UserId, TenantId = Tenant, Email = "ada@acme.com" };
            Users.Store[(Tenant, UserId)] = User;
            Service = new AccountSelfService(Users, Sessions, History, Hasher, Audit);
        }

        public Guid AddActiveSession()
        {
            var s = new Session
            {
                Id = Guid.NewGuid(), TenantId = Tenant, UserId = UserId, RefreshTokenHash = "x",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
            };
            Sessions.Store[s.Id] = s;
            return s.Id;
        }
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public readonly Dictionary<(Guid, Guid), User> Store = new();
        public Task<User?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Store.GetValueOrDefault((tenantId, id)));
        public Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
            => Task.FromResult(Store.Values.FirstOrDefault(u => u.TenantId == tenantId && u.Email == email));
        public Task<User?> GetByVerifiedPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default)
            => Task.FromResult(Store.Values.FirstOrDefault(u => u.TenantId == tenantId && u.PhoneVerified && u.Phone == normalizedPhone));
        public Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<User>>(Store.Values.Where(u => u.TenantId == tenantId).ToList());
        public Task<Core.Common.PagedResult<User>> ListPagedAsync(Guid tenantId, Core.Common.Pagination page, string? emailContains = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task DeleteAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct = default) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken ct = default) { Store[(user.TenantId, user.Id)] = user; return Task.CompletedTask; }
        public Task UpdateAsync(User user, CancellationToken ct = default) { Store[(user.TenantId, user.Id)] = user; return Task.CompletedTask; }
    }

    private sealed class FakeSessionRepo : ISessionRepository
    {
        public readonly Dictionary<Guid, Session> Store = new();
        public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Store.GetValueOrDefault(id));
        public Task<Session?> GetByRefreshTokenHashAsync(string refreshTokenHash, CancellationToken ct = default)
            => Task.FromResult(Store.Values.FirstOrDefault(s => s.RefreshTokenHash == refreshTokenHash));
        public Task<IReadOnlyList<Session>> ListActiveForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Session>>(
                Store.Values.Where(s => s.TenantId == tenantId && s.UserId == userId && !s.Revoked).ToList());
        public Task AddAsync(Session session, CancellationToken ct = default) { Store[session.Id] = session; return Task.CompletedTask; }
        public Task UpdateAsync(Session session, CancellationToken ct = default) { Store[session.Id] = session; return Task.CompletedTask; }
        public Task<int> RevokeAllForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        {
            var n = 0;
            foreach (var s in Store.Values.Where(s => s.TenantId == tenantId && s.UserId == userId && !s.Revoked))
            { s.Revoked = true; n++; }
            return Task.FromResult(n);
        }
    }

    private sealed class FakeLoginHistoryRepo : ILoginHistoryRepository
    {
        public Task AddAsync(LoginHistory entry, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<LoginHistory>> ListForUserAsync(Guid tenantId, Guid userId, int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoginHistory>>(Array.Empty<LoginHistory>());
    }

    // A trivial reversible "hasher" — enough to test verify/replace semantics without Argon2 cost.
    private sealed class FakeHasher : IPasswordHasher
    {
        public string Hash(string password) => "H:" + password;
        public bool Verify(string encodedHash, string password) => encodedHash == "H:" + password;
    }

    private sealed class RecordingAudit : IAuditLogger
    {
        public readonly List<string> Events = new();
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
            Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
        { Events.Add(@event); return Task.CompletedTask; }
    }
}
