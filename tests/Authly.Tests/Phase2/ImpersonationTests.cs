using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Authly.Modules.Users;

namespace Authly.Tests.Phase2;

public class ImpersonationTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid Admin = Guid.NewGuid();

    [Fact]
    public async Task Start_creates_session_and_audits()
    {
        var target = Active();
        var (svc, auth, audit) = Build(target);

        var result = await svc.StartAsync(Tenant, Admin, target.Id, RequestInfo.Unknown, AuditContext.System);

        Assert.Equal(target.Id, result.User.Id);
        Assert.Equal("impersonation", auth.LastMethod);
        Assert.Contains("user.impersonation_started", audit.Events);
    }

    [Fact]
    public async Task Cannot_impersonate_self()
    {
        var (svc, _, _) = Build(Active());
        await Assert.ThrowsAsync<ImpersonationNotAllowedException>(
            () => svc.StartAsync(Tenant, Admin, Admin, RequestInfo.Unknown, AuditContext.System));
    }

    [Fact]
    public async Task Cannot_impersonate_missing_user()
    {
        var (svc, _, _) = Build(target: null);
        await Assert.ThrowsAsync<ImpersonationNotAllowedException>(
            () => svc.StartAsync(Tenant, Admin, Guid.NewGuid(), RequestInfo.Unknown, AuditContext.System));
    }

    [Fact]
    public async Task Cannot_impersonate_suspended_user()
    {
        var suspended = Active();
        suspended.Status = UserStatus.Suspended;
        var (svc, _, _) = Build(suspended);
        await Assert.ThrowsAsync<ImpersonationNotAllowedException>(
            () => svc.StartAsync(Tenant, Admin, suspended.Id, RequestInfo.Unknown, AuditContext.System));
    }

    [Fact]
    public async Task Stop_revokes_session_and_audits()
    {
        var (svc, auth, audit) = Build(Active());
        var sid = Guid.NewGuid();

        await svc.StopAsync(Tenant, sid, AuditContext.System);

        Assert.Equal(sid, auth.RevokedSessionId);
        Assert.Contains("user.impersonation_stopped", audit.Events);
    }

    private static User Active() => new() { Id = Guid.NewGuid(), TenantId = Tenant, Email = "u@x.com", Status = UserStatus.Active };

    private static (ImpersonationService, FakeAuth, ImpersonationRecordingAudit) Build(User? target)
    {
        var auth = new FakeAuth();
        var audit = new ImpersonationRecordingAudit();
        var svc = new ImpersonationService(new FakeUserRepo(target), auth, audit);
        return (svc, auth, audit);
    }
}

internal sealed class FakeUserRepo : IUserRepository
{
    private readonly User? _user;
    public FakeUserRepo(User? user) => _user = user;
    public Task<User?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Task.FromResult(_user is not null && _user.Id == id ? _user : null);

    // Unused by these tests.
    public Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default) => Task.FromResult<User?>(null);
    public Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<User>>(Array.Empty<User>());
    public Task<Authly.Core.Common.PagedResult<User>> ListPagedAsync(Guid tenantId, Authly.Core.Common.Pagination page, string? emailContains = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
    public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> AnyTenantAdminAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(false);
    public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
    public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeAuth : IAuthService
{
    public string? LastMethod;
    public Guid? RevokedSessionId;

    public Task<Session> StartSessionAsync(User user, string method, RequestInfo info, CancellationToken ct = default)
    {
        LastMethod = method;
        return Task.FromResult(new Session { Id = Guid.NewGuid(), UserId = user.Id, TenantId = user.TenantId });
    }
    public Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default) { RevokedSessionId = sessionId; return Task.CompletedTask; }

    // Unused by these tests.
    public Task<User> RegisterAsync(Guid tenantId, RegisterRequest request, RequestInfo info, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<LoginResult> AuthenticateAsync(Guid tenantId, string email, string password, RequestInfo info, CancellationToken ct = default) => throw new NotImplementedException();
    public Task ResendVerificationEmailAsync(Guid tenantId, string email, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> VerifyEmailAsync(Guid tenantId, string rawToken, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RequestPasswordResetAsync(Guid tenantId, string email, RequestInfo info, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ResetPasswordAsync(Guid tenantId, string rawToken, string newPassword, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Session?> GetActiveSessionAsync(Guid sessionId, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class ImpersonationRecordingAudit : Authly.Modules.Audit.IAuditLogger
{
    public readonly List<string> Events = new();
    public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
        Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
    { Events.Add(@event); return Task.CompletedTask; }
}
