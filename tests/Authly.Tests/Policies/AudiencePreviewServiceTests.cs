using Authly.Core.Common;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Core.Policies;
using Authly.Modules.Policies;
using Xunit;

namespace Authly.Tests.Policies;

public sealed class AudiencePreviewServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid U1 = Guid.NewGuid();
    private static readonly Guid U2 = Guid.NewGuid();
    private static readonly Guid U3 = Guid.NewGuid();
    private static readonly Guid StaffRole = Guid.NewGuid();

    private readonly FakeUserRepo _users = new();
    private readonly FakeRoleRepo _roles = new();
    private readonly FakeUserRoleRepo _userRoles = new();
    private readonly FakeSocialIdentityRepo _social = new();
    private readonly AudiencePreviewService _sut;

    public AudiencePreviewServiceTests()
    {
        _users.Items.AddRange(new[]
        {
            new User { Id = U1, TenantId = Tenant, Email = "a@x.com" },
            new User { Id = U2, TenantId = Tenant, Email = "b@x.com" },
            new User { Id = U3, TenantId = Tenant, Email = "c@x.com" },
        });
        _roles.Items.Add(new Role { Id = StaffRole, TenantId = Tenant, Name = "staff" });
        _userRoles.UsersByRole[(Tenant, StaffRole)] = new() { U1, U2 };
        _social.Items.Add(new SocialIdentity { Id = Guid.NewGuid(), TenantId = Tenant, UserId = U3, Provider = "google", ProviderId = "g1" });

        _sut = new AudiencePreviewService(_users, _roles, _userRoles, _social);
    }

    [Fact]
    public async Task All_counts_every_user()
    {
        var p = await _sut.PreviewAsync(Tenant, new PolicyTargeting { Audience = Audiences.All });
        Assert.Equal(3, p.Count);
    }

    [Fact]
    public async Task Roles_counts_users_with_role()
    {
        var p = await _sut.PreviewAsync(Tenant, new PolicyTargeting { Audience = Audiences.Roles, Roles = new() { "staff" } });
        Assert.Equal(2, p.Count);
    }

    [Fact]
    public async Task Providers_counts_users_with_provider()
    {
        var p = await _sut.PreviewAsync(Tenant, new PolicyTargeting { Audience = Audiences.Providers, Providers = new() { "google" } });
        Assert.Equal(1, p.Count);
    }

    [Fact]
    public async Task Application_audience_returns_note_not_count()
    {
        var p = await _sut.PreviewAsync(Tenant, new PolicyTargeting { Audience = Audiences.Applications, ApplicationIds = new() { Guid.NewGuid() } });
        Assert.Null(p.Count);
        Assert.NotNull(p.Note);
    }

    [Fact]
    public async Task Advanced_all_intersects_static_dimensions()
    {
        // role staff = {U1,U2}; provider google = {U3}; intersection = {} for "all".
        var p = await _sut.PreviewAsync(Tenant, new PolicyTargeting
        {
            Audience = Audiences.Advanced, Match = "all",
            Roles = new() { "staff" }, Providers = new() { "google" }
        });
        Assert.Equal(0, p.Count);
    }

    [Fact]
    public async Task Advanced_any_unions_static_dimensions()
    {
        var p = await _sut.PreviewAsync(Tenant, new PolicyTargeting
        {
            Audience = Audiences.Advanced, Match = "any",
            Roles = new() { "staff" }, Providers = new() { "google" }
        });
        Assert.Equal(3, p.Count); // {U1,U2} ∪ {U3}
    }
}

// --- Minimal fakes for the repositories AudiencePreviewService needs ---

internal sealed class FakeUserRepo : IUserRepository
{
    public readonly List<User> Items = new();
    public Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<User>>(Items.Where(u => u.TenantId == tenantId).ToList());

    public Task<User?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<User?> GetByVerifiedPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<User?> GetByPhoneAsync(Guid tenantId, string normalizedPhone, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<PagedResult<User>> ListPagedAsync(Guid tenantId, Pagination page, string? emailContains = null, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteAsync(User user, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddAsync(User user, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateAsync(User user, CancellationToken ct = default) => throw new NotImplementedException();
}

internal sealed class FakeRoleRepo : IRoleRepository
{
    public readonly List<Role> Items = new();
    public Task<IReadOnlyList<Role>> ListRolesAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Role>>(Items.Where(r => r.TenantId == tenantId).ToList());
    public Task<Role?> GetRoleByNameAsync(Guid tenantId, string name, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(r => r.TenantId == tenantId && r.Name == name));

    public Task<Role?> GetRoleAsync(Guid tenantId, Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<bool> AnyRolesAsync(Guid tenantId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddRoleAsync(Role role, CancellationToken ct = default) => throw new NotImplementedException();
    public Task UpdateRoleAsync(Role role, CancellationToken ct = default) => throw new NotImplementedException();
    public Task DeleteRoleAsync(Role role, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Permission>> ListPermissionsAsync(Guid tenantId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<Permission?> GetPermissionAsync(Guid tenantId, string resource, string action, CancellationToken ct = default) => throw new NotImplementedException();
    public Task AddPermissionAsync(Permission permission, CancellationToken ct = default) => throw new NotImplementedException();
    public Task<IReadOnlyList<Guid>> ListPermissionIdsForRoleAsync(Guid roleId, CancellationToken ct = default) => throw new NotImplementedException();
    public Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default) => throw new NotImplementedException();
}
