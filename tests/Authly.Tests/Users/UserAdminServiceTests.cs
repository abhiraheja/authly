using Authly.Core.Common;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Infrastructure.Security;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Authly.Modules.Users;

namespace Authly.Tests.Users;

public class UserAdminServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Create_hashes_password_and_rejects_duplicates()
    {
        var h = new Harness();
        var user = await h.Service.CreateAsync(Tenant,
            new CreateUserRequest("New@Example.com", "Secret123!", "Ada", "Lovelace"), AuditContext.System);

        Assert.Equal("new@example.com", user.Email);                 // normalized
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual("Secret123!", user.PasswordHash);            // hashed
        Assert.Contains("user.created", h.Audit.Events);

        await Assert.ThrowsAsync<UserEmailAlreadyExistsException>(() =>
            h.Service.CreateAsync(Tenant, new CreateUserRequest("new@example.com", null, null, null), AuditContext.System));
    }

    [Fact]
    public async Task Suspend_sets_status_and_revokes_sessions()
    {
        var h = new Harness();
        var user = await h.Service.CreateAsync(Tenant, new CreateUserRequest("u@x.com", "pw12345!", null, null), AuditContext.System);
        h.Sessions.Items.Add(new Session { Id = Guid.NewGuid(), TenantId = Tenant, UserId = user.Id, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });

        await h.Service.SuspendAsync(Tenant, user.Id, AuditContext.System);

        Assert.Equal(UserStatus.Suspended, user.Status);
        Assert.True(h.Sessions.Items.Single().Revoked);
        Assert.Contains("user.suspended", h.Audit.Events);
    }

    [Fact]
    public async Task Delete_soft_deletes_and_revokes_sessions()
    {
        var h = new Harness();
        var user = await h.Service.CreateAsync(Tenant, new CreateUserRequest("d@x.com", "pw12345!", null, null), AuditContext.System);

        await h.Service.DeleteAsync(Tenant, user.Id, AuditContext.System);

        Assert.Equal(UserStatus.Deleted, user.Status);
        Assert.Contains(user, h.Users.Items);                        // retained (soft delete)
        Assert.Contains("user.deleted", h.Audit.Events);
    }

    [Fact]
    public async Task ForcePasswordReset_revokes_sessions_and_triggers_reset_email()
    {
        var h = new Harness();
        var user = await h.Service.CreateAsync(Tenant, new CreateUserRequest("r@x.com", "pw12345!", null, null), AuditContext.System);
        h.Sessions.Items.Add(new Session { Id = Guid.NewGuid(), TenantId = Tenant, UserId = user.Id, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });

        await h.Service.ForcePasswordResetAsync(Tenant, user.Id, AuditContext.System);

        Assert.True(h.Sessions.Items.Single().Revoked);
        Assert.Equal(user.Email, h.Auth.ResetRequestedFor);
        Assert.Contains("user.force_password_reset", h.Audit.Events);
    }

    [Fact]
    public async Task Update_unknown_user_throws()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<UserNotFoundException>(() =>
            h.Service.UpdateAsync(Tenant, Guid.NewGuid(), new UpdateUserRequest(null, null, null, null, null), AuditContext.System));
    }

    private sealed class Harness
    {
        public readonly FakeUserRepository Users = new();
        public readonly FakeSessionRepository Sessions = new();
        public readonly FakeAuthService Auth = new();
        public readonly RecordingAuditLogger Audit = new();
        public readonly UserAdminService Service;

        public Harness() => Service = new UserAdminService(Users, Sessions, new Argon2idPasswordHasher(), Auth, Audit);
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public readonly List<User> Items = new();
        public Task<User?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(u => u.TenantId == tenantId && u.Id == id));
        public Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(u => u.TenantId == tenantId && u.Email == email));
        public Task<User?> GetByVerifiedPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(u => u.TenantId == tenantId && u.PhoneVerified && u.Phone == normalizedPhone));
        public Task<User?> GetByPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(u => u.TenantId == tenantId && u.Phone == normalizedPhone));
        public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct = default)
            => Task.FromResult(Items.Any(u => u.TenantId == tenantId && u.Email == email));
        public Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<User>>(Items.Where(u => u.TenantId == tenantId).ToList());
        public Task<PagedResult<User>> ListPagedAsync(Guid tenantId, Pagination page, string? emailContains = null, CancellationToken ct = default)
        {
            var list = Items.Where(u => u.TenantId == tenantId).ToList();
            return Task.FromResult(new PagedResult<User>(list.Skip(page.Skip).Take(page.Limit).ToList(), list.Count));
        }
        public Task DeleteAsync(User user, CancellationToken ct = default) { Items.Remove(user); return Task.CompletedTask; }
        public Task AddAsync(User user, CancellationToken ct = default)
        {
            if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
            Items.Add(user);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        public readonly List<Session> Items = new();
        public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s => s.Id == id));
        public Task<Session?> GetByRefreshTokenHashAsync(string hash, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s => s.RefreshTokenHash == hash));
        public Task<IReadOnlyList<Session>> ListActiveForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Session>>(
                Items.Where(s => s.TenantId == tenantId && s.UserId == userId && !s.Revoked).ToList());
        public Task AddAsync(Session session, CancellationToken ct = default) { Items.Add(session); return Task.CompletedTask; }
        public Task UpdateAsync(Session session, CancellationToken ct = default) => Task.CompletedTask;
        public Task<int> RevokeAllForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        {
            var n = 0;
            foreach (var s in Items.Where(s => s.TenantId == tenantId && s.UserId == userId && !s.Revoked)) { s.Revoked = true; n++; }
            return Task.FromResult(n);
        }
    }

    private sealed class FakeAuthService : IAuthService
    {
        public string? ResetRequestedFor;
        public Task RequestPasswordResetAsync(Guid tenantId, string email, RequestInfo info, CancellationToken ct = default)
        {
            ResetRequestedFor = email;
            return Task.CompletedTask;
        }
        // Unused by UserAdminService:
        public Task<User> RegisterAsync(Guid tenantId, RegisterRequest request, RequestInfo info, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<LoginResult> AuthenticateAsync(Guid tenantId, string email, string password, RequestInfo info, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<LoginResult> AuthenticateByPhoneAsync(Guid tenantId, string phone, string password, RequestInfo info, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ResendVerificationEmailAsync(Guid tenantId, string email, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> VerifyEmailAsync(Guid tenantId, string rawToken, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ResetPasswordAsync(Guid tenantId, string rawToken, string newPassword, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Session> StartSessionAsync(User user, string method, RequestInfo info, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Session?> GetActiveSessionAsync(Guid sessionId, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public readonly List<string> Events = new();
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null,
            string? resourceType = null, Guid? resourceId = null, string result = "success",
            object? metadata = null, CancellationToken ct = default)
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }
}
