using Authly.Core.Authorization;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Authly.Modules.Members;
using Authly.Modules.Operators;
using AccountEntity = Authly.Core.Entities.Account;

namespace Authly.Tests.Members;

public class InvitationServiceTests
{
    [Fact]
    public async Task Invite_creates_account_membership_role_token_and_email()
    {
        var h = new Harness();
        var (org, _, viewerRoleId) = await h.SeedOrgWithProjectAsync();

        await h.Invites.InviteAsync(org, h.ProjectId, "New.Person@Example.com", new[] { viewerRoleId }, AuditContext.System);

        var account = h.Accounts.All.Single();
        Assert.Equal("new.person@example.com", account.Email);          // normalized
        Assert.Null(account.PasswordHash);                              // pending — cannot sign in yet
        Assert.False(account.EmailVerified);

        var membership = h.Memberships.All.Single();
        Assert.Equal(MembershipStatus.Invited, membership.Status);
        Assert.Single(h.MemberRoles.Assignments, a => a.OperatorRoleId == viewerRoleId);
        Assert.Single(h.Tokens.All, t => !t.Used);
        Assert.Single(h.Queue.Sent, m => m.TemplateKey == "operator_invite" && m.Recipient == "new.person@example.com");
    }

    [Fact]
    public async Task Accept_sets_password_verifies_and_activates_membership_single_use()
    {
        var h = new Harness();
        var (org, _, viewerRoleId) = await h.SeedOrgWithProjectAsync();
        await h.Invites.InviteAsync(org, h.ProjectId, "p@example.com", new[] { viewerRoleId }, AuditContext.System);
        var raw = h.Queue.Sent.Single().Variables["action_url"]; // FakeUrlBuilder returns the raw token as the url

        var result = await h.Invites.AcceptAsync(raw, "S3cretpw!", RequestInfo.Unknown);

        Assert.NotNull(result);
        Assert.Equal(org, result!.OrganizationId);
        Assert.Equal(h.ProjectId, result.ProjectId);

        var account = h.Accounts.All.Single();
        Assert.False(string.IsNullOrEmpty(account.PasswordHash));
        Assert.True(account.EmailVerified);
        Assert.Equal(MembershipStatus.Active, h.Memberships.All.Single().Status);

        // Token is single-use — replay fails.
        Assert.Null(await h.Invites.AcceptAsync(raw, "S3cretpw!", RequestInfo.Unknown));
    }

    [Fact]
    public async Task Reinviting_an_active_member_is_rejected()
    {
        var h = new Harness();
        var (org, _, viewerRoleId) = await h.SeedOrgWithProjectAsync();
        await h.Invites.InviteAsync(org, h.ProjectId, "p@example.com", new[] { viewerRoleId }, AuditContext.System);
        var raw = h.Queue.Sent.Single().Variables["action_url"];
        await h.Invites.AcceptAsync(raw, "S3cretpw!", RequestInfo.Unknown);

        await Assert.ThrowsAsync<InviteAccountException>(
            () => h.Invites.InviteAsync(org, h.ProjectId, "p@example.com", new[] { viewerRoleId }, AuditContext.System));
    }

    [Fact]
    public async Task Accept_with_invalid_token_returns_null()
    {
        var h = new Harness();
        await h.SeedOrgWithProjectAsync();
        Assert.Null(await h.Invites.AcceptAsync("nope", "pw", RequestInfo.Unknown));
    }
}

public class MemberDirectoryServiceTests
{
    [Fact]
    public async Task Lists_members_with_roles_and_owner_flag()
    {
        var h = new Harness();
        var (org, ownerMembershipId, _) = await h.SeedOrgWithProjectAsync(seedOwner: true);

        var rows = await h.Directory.ListMembersAsync(org);
        var owner = rows.Single(r => r.MembershipId == ownerMembershipId);
        Assert.True(owner.IsOwner);
        Assert.Contains(OperatorRbac.OrgOwner, owner.RoleNames);
    }

    [Fact]
    public async Task Cannot_remove_the_last_owner()
    {
        var h = new Harness();
        var (org, ownerMembershipId, _) = await h.SeedOrgWithProjectAsync(seedOwner: true);
        await Assert.ThrowsAsync<LastOwnerProtectedException>(
            () => h.Directory.RemoveMemberAsync(org, ownerMembershipId, AuditContext.System));
    }

    [Fact]
    public async Task Removing_a_member_disables_them_and_clears_roles()
    {
        var h = new Harness();
        var (org, _, viewerRoleId) = await h.SeedOrgWithProjectAsync(seedOwner: true);
        await h.Invites.InviteAsync(org, h.ProjectId, "viewer@example.com", new[] { viewerRoleId }, AuditContext.System);
        var account = h.Accounts.All.Single(a => a.Email == "viewer@example.com");
        var membership = h.Memberships.All.Single(m => m.AccountId == account.Id);

        await h.Directory.RemoveMemberAsync(org, membership.Id, AuditContext.System);

        Assert.Equal(MembershipStatus.Disabled, membership.Status);
        Assert.DoesNotContain(h.MemberRoles.Assignments, a => a.OrganizationMembershipId == membership.Id);
    }
}

// --- Test harness wiring the real services over in-memory fakes ---

internal sealed class Harness
{
    public readonly FakeAccountRepo Accounts = new();
    public readonly FakeOrgRepo Orgs = new();
    public readonly FakeMembershipRepo Memberships;
    public readonly FakeMemberRoleRepo MemberRoles;
    public readonly FakeOperatorRoleRepo Roles = new();
    public readonly FakeInviteTokenRepo Tokens = new();
    public readonly FakeTenantRepo Tenants = new();
    public readonly FakeQueue Queue = new();
    public readonly OperatorRbacService Rbac;
    public readonly InvitationService Invites;
    public readonly MemberDirectoryService Directory;
    public Guid ProjectId { get; private set; }

    public Harness()
    {
        MemberRoles = new FakeMemberRoleRepo(Roles);
        Memberships = new FakeMembershipRepo();
        var audit = new NoopMembersAudit();
        Rbac = new OperatorRbacService(Roles, MemberRoles, audit);
        Invites = new InvitationService(Accounts, Orgs, Memberships, MemberRoles, Roles, Rbac, Tokens, Tenants,
            new FakeTokenHasher(), new FakePasswordHasher(), Queue, new FakeUrlBuilder(), audit);
        Directory = new MemberDirectoryService(Memberships, MemberRoles, Roles, audit);
    }

    /// <summary>Seeds an org + project + system operator roles; optionally a founding owner member.</summary>
    public async Task<(Guid OrgId, Guid OwnerMembershipId, Guid ViewerRoleId)> SeedOrgWithProjectAsync(bool seedOwner = false)
    {
        var org = new Organization { Id = Guid.NewGuid(), Name = "Acme", Slug = "acme" };
        Orgs.All.Add(org);
        ProjectId = Guid.NewGuid();
        Tenants.All.Add(new Tenant { Id = ProjectId, OrganizationId = org.Id, Name = "Prod", Slug = "acme-prod" });
        await Rbac.EnsureSystemRolesAsync(org.Id);

        var ownerMembershipId = Guid.Empty;
        if (seedOwner)
        {
            var ownerAccount = new AccountEntity { Id = Guid.NewGuid(), Email = "owner@example.com", PasswordHash = "x", Status = AccountStatus.Active };
            Accounts.All.Add(ownerAccount);
            var membership = new OrganizationMembership { Id = Guid.NewGuid(), AccountId = ownerAccount.Id, OrganizationId = org.Id, Status = MembershipStatus.Active, Account = ownerAccount };
            Memberships.All.Add(membership);
            ownerMembershipId = membership.Id;
            await Rbac.AssignSystemRoleAsync(org.Id, membership.Id, OperatorRbac.OrgOwner, ownerAccount.Id);
        }

        var viewer = Roles.Roles.Single(r => r.OrganizationId == org.Id && r.Name == OperatorRbac.Viewer);
        return (org.Id, ownerMembershipId, viewer.Id);
    }
}

internal sealed class FakeAccountRepo : IAccountRepository
{
    public readonly List<AccountEntity> All = new();
    public Task<AccountEntity?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(All.FirstOrDefault(a => a.Id == id));
    public Task<AccountEntity?> GetByEmailAsync(string email, CancellationToken ct = default) => Task.FromResult(All.FirstOrDefault(a => a.Email == email));
    public Task<bool> EmailExistsAsync(string email, CancellationToken ct = default) => Task.FromResult(All.Any(a => a.Email == email));
    public Task AddAsync(AccountEntity account, CancellationToken ct = default) { if (account.Id == Guid.Empty) account.Id = Guid.NewGuid(); All.Add(account); return Task.CompletedTask; }
    public Task UpdateAsync(AccountEntity account, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeOrgRepo : IOrganizationRepository
{
    public readonly List<Organization> All = new();
    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(All.FirstOrDefault(o => o.Id == id));
    public Task<Organization?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(All.FirstOrDefault(o => o.Slug == slug));
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => Task.FromResult(All.Any(o => o.Slug == slug));
    public Task AddAsync(Organization organization, CancellationToken ct = default) { All.Add(organization); return Task.CompletedTask; }
    public Task UpdateAsync(Organization organization, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(Organization organization, CancellationToken ct = default) { All.Remove(organization); return Task.CompletedTask; }
}

internal sealed class FakeMembershipRepo : IOrganizationMembershipRepository
{
    public readonly List<OrganizationMembership> All = new();
    public Task<OrganizationMembership?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(All.FirstOrDefault(m => m.Id == id));
    public Task<OrganizationMembership?> GetAsync(Guid accountId, Guid organizationId, CancellationToken ct = default)
        => Task.FromResult(All.FirstOrDefault(m => m.AccountId == accountId && m.OrganizationId == organizationId));
    public Task<IReadOnlyList<OrganizationMembership>> ListByAccountAsync(Guid accountId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OrganizationMembership>>(All.Where(m => m.AccountId == accountId).ToList());
    public Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OrganizationMembership>>(All.Where(m => m.OrganizationId == organizationId).ToList());
    public Task<IReadOnlyList<OrganizationMembership>> ListByOrganizationWithAccountsAsync(Guid organizationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OrganizationMembership>>(All.Where(m => m.OrganizationId == organizationId).ToList());
    public Task AddAsync(OrganizationMembership m, CancellationToken ct = default) { if (m.Id == Guid.Empty) m.Id = Guid.NewGuid(); All.Add(m); return Task.CompletedTask; }
    public Task UpdateAsync(OrganizationMembership m, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeMemberRoleRepo : IMemberRoleRepository
{
    private readonly FakeOperatorRoleRepo _roles;
    public readonly List<MemberRole> Assignments = new();
    public FakeMemberRoleRepo(FakeOperatorRoleRepo roles) => _roles = roles;

    public Task AssignAsync(MemberRole a, CancellationToken ct = default)
    {
        if (!Assignments.Any(x => x.OrganizationMembershipId == a.OrganizationMembershipId && x.OperatorRoleId == a.OperatorRoleId))
            Assignments.Add(a);
        return Task.CompletedTask;
    }
    public Task RemoveAsync(Guid membershipId, Guid roleId, CancellationToken ct = default)
    { Assignments.RemoveAll(m => m.OrganizationMembershipId == membershipId && m.OperatorRoleId == roleId); return Task.CompletedTask; }
    public Task RemoveAllForMembershipAsync(Guid membershipId, CancellationToken ct = default)
    { Assignments.RemoveAll(m => m.OrganizationMembershipId == membershipId); return Task.CompletedTask; }
    public Task<IReadOnlyList<OperatorRole>> ListRolesForMembershipAsync(Guid membershipId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OperatorRole>>(Assignments.Where(m => m.OrganizationMembershipId == membershipId)
            .Select(m => _roles.Roles.First(r => r.Id == m.OperatorRoleId)).ToList());
    public Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid membershipId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Assignments.Where(m => m.OrganizationMembershipId == membershipId)
            .Select(m => _roles.Roles.First(r => r.Id == m.OperatorRoleId).Name).Distinct().ToList());
    public Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid membershipId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    public Task<int> CountMembershipsWithRoleAsync(Guid org, Guid roleId, CancellationToken ct = default)
        => Task.FromResult(Assignments.Where(m => m.OrganizationId == org && m.OperatorRoleId == roleId).Select(m => m.OrganizationMembershipId).Distinct().Count());
}

internal sealed class FakeOperatorRoleRepo : IOperatorRoleRepository
{
    public readonly List<OperatorRole> Roles = new();
    public readonly List<OperatorPermission> Permissions = new();
    public readonly Dictionary<Guid, List<Guid>> RolePermissions = new();

    public Task<IReadOnlyList<OperatorRole>> ListRolesAsync(Guid org, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OperatorRole>>(Roles.Where(r => r.OrganizationId == org).ToList());
    public Task<OperatorRole?> GetRoleAsync(Guid org, Guid id, CancellationToken ct = default)
        => Task.FromResult(Roles.FirstOrDefault(r => r.OrganizationId == org && r.Id == id));
    public Task<OperatorRole?> GetRoleByNameAsync(Guid org, string name, CancellationToken ct = default)
        => Task.FromResult(Roles.FirstOrDefault(r => r.OrganizationId == org && r.Name == name));
    public Task AddRoleAsync(OperatorRole role, CancellationToken ct = default) { if (role.Id == Guid.Empty) role.Id = Guid.NewGuid(); Roles.Add(role); return Task.CompletedTask; }
    public Task UpdateRoleAsync(OperatorRole role, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteRoleAsync(OperatorRole role, CancellationToken ct = default) { Roles.Remove(role); return Task.CompletedTask; }
    public Task<IReadOnlyList<OperatorPermission>> ListPermissionsAsync(Guid org, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<OperatorPermission>>(Permissions.Where(p => p.OrganizationId == org).ToList());
    public Task AddPermissionAsync(OperatorPermission permission, CancellationToken ct = default) { if (permission.Id == Guid.Empty) permission.Id = Guid.NewGuid(); Permissions.Add(permission); return Task.CompletedTask; }
    public Task<IReadOnlyList<Guid>> ListPermissionIdsForRoleAsync(Guid roleId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Guid>>(RolePermissions.TryGetValue(roleId, out var l) ? l : new List<Guid>());
    public Task SetRolePermissionsAsync(Guid roleId, IReadOnlyCollection<Guid> permissionIds, CancellationToken ct = default)
    { RolePermissions[roleId] = permissionIds.Distinct().ToList(); return Task.CompletedTask; }
}

internal sealed class FakeInviteTokenRepo : IAccountInviteTokenRepository
{
    public readonly List<AccountInviteToken> All = new();
    public Task<AccountInviteToken?> GetByHashAsync(string tokenHash, CancellationToken ct = default)
        => Task.FromResult(All.FirstOrDefault(t => t.TokenHash == tokenHash));
    public Task AddAsync(AccountInviteToken token, CancellationToken ct = default) { if (token.Id == Guid.Empty) token.Id = Guid.NewGuid(); All.Add(token); return Task.CompletedTask; }
    public Task UpdateAsync(AccountInviteToken token, CancellationToken ct = default) => Task.CompletedTask;
    public Task InvalidateOutstandingAsync(Guid accountId, Guid organizationId, CancellationToken ct = default)
    { foreach (var t in All.Where(t => t.AccountId == accountId && t.OrganizationId == organizationId && !t.Used)) t.Used = true; return Task.CompletedTask; }
}

internal sealed class FakeTenantRepo : ITenantRepository
{
    public readonly List<Tenant> All = new();
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(All.FirstOrDefault(t => t.Id == id));
    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(All.FirstOrDefault(t => t.Slug == slug));
    public Task<Tenant?> GetByCustomDomainOrNullAsync(string host, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
    public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>(All.ToList());
    public Task<IReadOnlyList<Tenant>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Tenant>>(All.Where(t => t.OrganizationId == organizationId).ToList());
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => Task.FromResult(All.Any(t => t.Slug == slug));
    public Task AddAsync(Tenant tenant, CancellationToken ct = default) { All.Add(tenant); return Task.CompletedTask; }
    public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeTokenHasher : ITokenHasher
{
    private int _n;
    public string GenerateRawToken() => $"raw-token-{++_n}";
    public string Hash(string rawToken) => $"hash:{rawToken}";
}

internal sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed:{password}";
    public bool Verify(string hash, string password) => hash == $"hashed:{password}";
}

internal sealed class FakeQueue : IMessageQueue
{
    public readonly List<MessageSendRequest> Sent = new();
    public void Enqueue(MessageSendRequest request) => Sent.Add(request);
}

// FakeUrlBuilder returns the raw token AS the url so tests can recover it for AcceptAsync.
internal sealed class FakeUrlBuilder : IAuthUrlBuilder
{
    public string BuildEmailVerificationUrl(Guid tenantId, string rawToken) => rawToken;
    public string BuildPasswordResetUrl(Guid tenantId, string rawToken) => rawToken;
    public string BuildMagicLinkUrl(Guid tenantId, string rawToken, string? returnUrl = null) => rawToken;
    public string BuildContactChangeVerifyUrl(Guid tenantId, string rawToken) => rawToken;
    public string BuildContactChangeCancelUrl(Guid tenantId, string rawToken) => rawToken;
    public string BuildRecoveryUrl(Guid tenantId, string rawToken) => rawToken;
    public string BuildInviteAcceptUrl(string rawToken) => rawToken;
}

internal sealed class NoopMembersAudit : IAuditLogger
{
    public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
        Guid? resourceId = null, string result = "success", object? metadata = null, bool publishEvent = true, CancellationToken ct = default)
        => Task.CompletedTask;
}
