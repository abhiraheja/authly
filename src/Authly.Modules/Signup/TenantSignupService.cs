using Authly.Core.Authorization;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using AccountEntity = Authly.Core.Entities.Account;
using Authly.Modules.Audit;
using Authly.Modules.Operators;
using Authly.Modules.Provisioning;
using Authly.Modules.Auth;
using Authly.Modules.Authorization;
using Authly.Modules.Common;
using Authly.Modules.Tenants;

namespace Authly.Modules.Signup;

/// <summary>Self-service signup input: a new organization plus its founding owner account.</summary>
public sealed record TenantSignupRequest(
    string CompanyName,
    string Email,
    string Password,
    string? FirstName = null,
    string? LastName = null);

/// <summary>The provisioned organization, its first project, and the founding owner account.</summary>
public sealed record TenantSignupResult(AccountEntity Account, Organization Organization, Tenant Tenant);

/// <summary>Thrown when a workspace cannot be provisioned (e.g. a unique slug could not be derived).</summary>
public sealed class TenantSignupException : Exception
{
    public TenantSignupException(string message) : base(message) { }
}

/// <summary>
/// Public, self-service onboarding (the Supabase / Google-Console model): a visitor creates a global
/// console <see cref="Account"/>, an <see cref="Organization"/>, and its first project (<see cref="Tenant"/>)
/// in one step, becoming the org owner. No super-admin involvement. Operators are the Account layer —
/// never tenant end-users (<see cref="User"/>).
/// </summary>
public interface ITenantSignupService
{
    Task<TenantSignupResult> SignUpAsync(TenantSignupRequest request, RequestInfo info, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class TenantSignupService : ITenantSignupService
{
    // Bound on slug de-duplication so a pathological name can't loop forever.
    private const int MaxSlugAttempts = 50;

    private readonly IAccountRepository _accounts;
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IConsoleProvisioningService _provisioning;
    private readonly IOperatorRbacService _operatorRbac;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogger _audit;

    public TenantSignupService(
        IAccountRepository accounts,
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships,
        IConsoleProvisioningService provisioning,
        IOperatorRbacService operatorRbac,
        IPasswordHasher hasher,
        IAuditLogger audit)
    {
        _accounts = accounts;
        _organizations = organizations;
        _memberships = memberships;
        _provisioning = provisioning;
        _operatorRbac = operatorRbac;
        _hasher = hasher;
        _audit = audit;
    }

    public async Task<TenantSignupResult> SignUpAsync(TenantSignupRequest request, RequestInfo info, CancellationToken ct = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        // 1) Global account (operator login). Email is unique across the whole platform.
        if (await _accounts.EmailExistsAsync(email, ct))
            throw new EmailAlreadyExistsException(email);

        var now = DateTimeOffset.UtcNow;
        var account = new AccountEntity
        {
            Email = email,
            PasswordHash = _hasher.Hash(request.Password),
            FirstName = request.FirstName,
            LastName = request.LastName,
            EmailVerified = false,
            Status = AccountStatus.Active,
            CreatedAt = now
        };
        await _accounts.AddAsync(account, ct);

        var actor = new AuditContext(account.Id, "account", info.IpAddress, info.UserAgent);
        await _audit.LogAsync("account.created", actor,
            resourceType: "account", resourceId: account.Id, metadata: new { account.Email }, ct: ct);

        // 2) Organization owned by that account (slug de-duplicated globally).
        var organization = await CreateOrganizationAsync(request.CompanyName, account.Id, ct);
        await _audit.LogAsync("organization.created", actor,
            resourceType: "organization", resourceId: organization.Id,
            metadata: new { organization.Slug, organization.Name }, ct: ct);

        // 3) Founding membership — the owner is immediately Active.
        var membership = new OrganizationMembership
        {
            AccountId = account.Id,
            OrganizationId = organization.Id,
            Status = MembershipStatus.Active,
            CreatedAt = now
        };
        await _memberships.AddAsync(membership, ct);

        // 4) Seed the org's operator-RBAC catalogue + system roles, and grant the founder org_owner.
        await _operatorRbac.EnsureSystemRolesAsync(organization.Id, ct);
        await _operatorRbac.AssignSystemRoleAsync(organization.Id, membership.Id, OperatorRbac.OrgOwner, account.Id, ct);

        // 5) First project (tenant) inside the org — slug de-dup + end-user system roles + RLS binding.
        var tenant = await _provisioning.CreateProjectAsync(organization.Id, request.CompanyName, actor, ct);

        await _audit.LogAsync("tenant.signup", actor, tenant.Id,
            resourceType: "tenant", resourceId: tenant.Id,
            metadata: new { tenant.Slug, tenant.Name, organization.Id }, ct: ct);

        return new TenantSignupResult(account, organization, tenant);
    }

    private async Task<Organization> CreateOrganizationAsync(string companyName, Guid ownerAccountId, CancellationToken ct)
    {
        var baseSlug = TenantService.Slugify(companyName);
        var now = DateTimeOffset.UtcNow;
        for (var attempt = 0; attempt < MaxSlugAttempts; attempt++)
        {
            var slug = attempt == 0 ? baseSlug : $"{baseSlug}-{attempt + 1}";
            if (await _organizations.SlugExistsAsync(slug, ct)) continue;

            var org = new Organization
            {
                Name = companyName.Trim(),
                Slug = slug,
                OwnerAccountId = ownerAccountId,
                CreatedAt = now,
                UpdatedAt = now
            };
            await _organizations.AddAsync(org, ct);
            return org;
        }

        throw new TenantSignupException("Could not allocate a unique organization identifier. Please try a different company name.");
    }
}
