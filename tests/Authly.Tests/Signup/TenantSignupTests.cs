using Authly.Core.Authorization;
using Authly.Core.Common;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Authorization;
using Authly.Modules.Common;
using Authly.Modules.Signup;
using Authly.Modules.Tenants;

namespace Authly.Tests.Signup;

public class TenantSignupTests
{
    private static (TenantSignupService svc, FakeTenantService tenants, FakeTenantContext ctx,
        FakeSignupUserRepo users, FakeSignupAuth auth, FakeSignupRbac rbac, FakeSignupAudit audit) Build()
    {
        var tenants = new FakeTenantService();
        var ctx = new FakeTenantContext();
        var users = new FakeSignupUserRepo();
        var auth = new FakeSignupAuth(ctx);
        var rbac = new FakeSignupRbac();
        var roles = new FakeSignupRoleRepo();
        var audit = new FakeSignupAudit();
        var svc = new TenantSignupService(tenants, ctx, auth, users, rbac, roles, audit);
        return (svc, tenants, ctx, users, auth, rbac, audit);
    }

    private static TenantSignupRequest Req(string company = "Acme Inc.", string email = "owner@acme.test")
        => new(company, email, "Sup3rSecret!", "Ada", "Lovelace");

    [Fact]
    public async Task Provisions_workspace_and_promotes_first_user_to_admin()
    {
        var (svc, _, ctx, users, _, rbac, audit) = Build();

        var result = await svc.SignUpAsync(Req(), new RequestInfo("1.2.3.4", "agent"));

        Assert.Equal("acme-inc", result.Tenant.Slug);
        Assert.True(result.User.IsTenantAdmin);
        Assert.Contains(result.User.Id, users.Updated);                 // promotion persisted
        Assert.Contains(result.Tenant.Id, rbac.SeededTenants);          // system roles seeded
        Assert.Contains(result.User.Id, rbac.RoleAssignments.Keys);     // tenant_admin granted
        Assert.Equal(result.Tenant.Id, ctx.TenantId);                   // RLS context bound to new tenant
        Assert.Contains("tenant.signup", audit.Events);
    }

    [Fact]
    public async Task Binds_tenant_context_before_creating_the_user()
    {
        var (svc, _, _, _, auth, _, _) = Build();

        await svc.SignUpAsync(Req(), new RequestInfo(null, null));

        // The user must have been created while a tenant was already in scope (RLS would reject otherwise).
        Assert.True(auth.TenantInScopeAtRegister);
    }

    [Fact]
    public async Task Disambiguates_a_taken_slug()
    {
        var (svc, tenants, _, _, _, _, _) = Build();
        tenants.TakenSlugs.Add("acme-inc");        // first choice already exists

        var result = await svc.SignUpAsync(Req(), new RequestInfo(null, null));

        Assert.Equal("acme-inc-2", result.Tenant.Slug);
    }

    [Fact]
    public async Task Gives_up_with_a_clear_error_when_no_slug_is_free()
    {
        var (svc, tenants, _, _, _, _, _) = Build();
        tenants.RejectEverything = true;

        await Assert.ThrowsAsync<TenantSignupException>(() => svc.SignUpAsync(Req(), new RequestInfo(null, null)));
    }
}

// --- Fakes (interface-focused; unused members throw to surface accidental use) ---

internal sealed class FakeTenantContext : ITenantContext
{
    public Guid? TenantId { get; private set; }
    public bool HasTenant => TenantId.HasValue;
    public void SetTenant(Guid tenantId) => TenantId = tenantId;
}

internal sealed class FakeTenantService : ITenantService
{
    public readonly HashSet<string> TakenSlugs = new();
    public bool RejectEverything;

    public Task<Tenant> CreateAsync(CreateTenantRequest request, AuditContext actor, CancellationToken ct = default)
    {
        var slug = request.Slug!;
        if (RejectEverything || !TakenSlugs.Add(slug))
            throw new SlugAlreadyExistsException(slug);
        return Task.FromResult(new Tenant
        {
            Id = Guid.NewGuid(), Name = request.Name, Slug = slug,
            Status = TenantStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        });
    }

    public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Tenant?> GetAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SuspendAsync(Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task ReactivateAsync(Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> IsOnboardedAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SetOnboardedAsync(Guid id, AuditContext actor, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeSignupAuth : IAuthService
{
    private readonly FakeTenantContext _ctx;
    public bool TenantInScopeAtRegister { get; private set; }
    public FakeSignupAuth(FakeTenantContext ctx) => _ctx = ctx;

    public Task<User> RegisterAsync(Guid tenantId, RegisterRequest request, RequestInfo info, CancellationToken ct = default)
    {
        TenantInScopeAtRegister = _ctx.HasTenant;
        var user = new User
        {
            Id = Guid.NewGuid(), TenantId = tenantId, Email = request.Email,
            FirstName = request.FirstName, LastName = request.LastName,
            Status = UserStatus.Active, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow
        };
        return Task.FromResult(user);
    }

    public Task<LoginResult> AuthenticateAsync(Guid t, string e, string p, RequestInfo i, CancellationToken ct = default) => throw new NotImplementedException();
    public Task ResendVerificationEmailAsync(Guid t, string e, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> VerifyEmailAsync(Guid t, string r, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RequestPasswordResetAsync(Guid t, string e, RequestInfo i, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> ResetPasswordAsync(Guid t, string r, string n, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Session> StartSessionAsync(User u, string m, RequestInfo i, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Session?> GetActiveSessionAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RevokeSessionAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeSignupUserRepo : IUserRepository
{
    public readonly List<Guid> Updated = new();

    public Task UpdateAsync(User user, CancellationToken ct = default) { Updated.Add(user.Id); return Task.CompletedTask; }

    public Task<User?> GetByIdAsync(Guid t, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<User?> GetByEmailAsync(Guid t, string e, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<User>> ListByTenantAsync(Guid t, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<PagedResult<User>> ListPagedAsync(Guid t, Pagination p, string? e = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(User user, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> EmailExistsAsync(Guid t, string e, CancellationToken ct = default) => Task.FromResult(false);
    public Task<bool> AnyTenantAdminAsync(Guid t, CancellationToken ct = default) => Task.FromResult(false);
    public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeSignupRbac : IRbacService
{
    public readonly List<Guid> SeededTenants = new();
    public readonly Dictionary<Guid, Guid> RoleAssignments = new();

    public Task EnsureSystemRolesAsync(Guid tenantId, CancellationToken ct = default) { SeededTenants.Add(tenantId); return Task.CompletedTask; }
    public Task AssignRoleAsync(Guid tenantId, Guid userId, Guid roleId, AuditContext actor, CancellationToken ct = default)
    { RoleAssignments[userId] = roleId; return Task.CompletedTask; }

    public Task<IReadOnlyList<Role>> ListRolesAsync(Guid t, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<RoleWithPermissions?> GetRoleAsync(Guid t, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Role> CreateRoleAsync(Guid t, CreateRoleRequest r, AuditContext a, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SetRolePermissionsAsync(Guid t, Guid r, IReadOnlyCollection<Guid> p, AuditContext a, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteRoleAsync(Guid t, Guid r, AuditContext a, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Permission>> ListPermissionsAsync(Guid t, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Role>> ListUserRolesAsync(Guid t, Guid u, CancellationToken ct = default) => throw new NotImplementedException();
    public Task RemoveRoleAsync(Guid t, Guid u, Guid r, AuditContext a, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<UserAuthorization> GetUserAuthorizationAsync(Guid t, Guid u, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeSignupRoleRepo : IRoleRepository
{
    public Task<Role?> GetRoleByNameAsync(Guid tenantId, string name, CancellationToken ct = default)
        => Task.FromResult<Role?>(name == SystemRbac.TenantAdmin
            ? new Role { Id = Guid.NewGuid(), TenantId = tenantId, Name = name, IsSystem = true }
            : null);

    public Task<IReadOnlyList<Role>> ListRolesAsync(Guid t, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Role?> GetRoleAsync(Guid t, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyRolesAsync(Guid t, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRoleAsync(Role r, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRoleAsync(Role r, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteRoleAsync(Role r, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Permission>> ListPermissionsAsync(Guid t, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Permission?> GetPermissionAsync(Guid t, string res, string act, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddPermissionAsync(Permission p, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Guid>> ListPermissionIdsForRoleAsync(Guid r, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SetRolePermissionsAsync(Guid r, IReadOnlyCollection<Guid> p, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeSignupAudit : IAuditLogger
{
    public readonly List<string> Events = new();
    public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
        Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
    { Events.Add(@event); return Task.CompletedTask; }
}
