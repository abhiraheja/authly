using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Authly.Modules.Messaging;
using Authly.Modules.Operators;
using AccountEntity = Authly.Core.Entities.Account;

namespace Authly.Modules.Members;

/// <summary>The result of accepting an invite: the now-Active account and the workspace it joins.
/// The web layer turns this into the tenant-admin sign-in cookie (same shape as signup/login).</summary>
public sealed record AcceptInviteResult(AccountEntity Account, Guid OrganizationId, Guid ProjectId);

public sealed class InviteAccountException(string message) : Exception(message);

/// <summary>
/// Employee (operator) invitations: invite an email into an organization with operator roles, and the
/// single-use token accept flow that activates the membership. Operates entirely on the global Account /
/// Organization layer — never on tenant end-users (<c>User</c>); end-user invites stay on
/// <c>IUserAdminService</c> (doc 06 §7).
/// </summary>
public interface IInvitationService
{
    /// <summary>Find-or-creates the invitee's global account, creates/reuses an Invited membership with
    /// the chosen operator roles, issues a single-use token, and emails the accept link through the
    /// org's active project (<paramref name="projectIdForEmail"/>) messaging provider.</summary>
    Task InviteAsync(Guid organizationId, Guid projectIdForEmail, string email,
        IReadOnlyCollection<Guid> operatorRoleIds, AuditContext actor, CancellationToken ct = default);

    /// <summary>Loads a pending (unused, unexpired) invite by its raw token — for rendering the accept
    /// page — or null when the token is invalid.</summary>
    Task<AccountInviteToken?> FindPendingAsync(string rawToken, CancellationToken ct = default);

    /// <summary>Consumes the token: sets the account's password (when it has none yet) and email-verified
    /// flag, flips the membership to Active, and returns the workspace to sign into. Null if invalid.</summary>
    Task<AcceptInviteResult?> AcceptAsync(string rawToken, string newPassword, RequestInfo info, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class InvitationService : IInvitationService
{
    private static readonly TimeSpan InviteLifetime = TimeSpan.FromDays(7);

    private readonly IAccountRepository _accounts;
    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly IMemberRoleRepository _memberRoles;
    private readonly IOperatorRoleRepository _operatorRoles;
    private readonly IOperatorRbacService _operatorRbac;
    private readonly IAccountInviteTokenRepository _tokens;
    private readonly ITenantRepository _tenants;
    private readonly ITokenHasher _tokenHasher;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IMessageQueue _messages;
    private readonly IAuthUrlBuilder _urls;
    private readonly IAuditLogger _audit;

    public InvitationService(
        IAccountRepository accounts,
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships,
        IMemberRoleRepository memberRoles,
        IOperatorRoleRepository operatorRoles,
        IOperatorRbacService operatorRbac,
        IAccountInviteTokenRepository tokens,
        ITenantRepository tenants,
        ITokenHasher tokenHasher,
        IPasswordHasher passwordHasher,
        IMessageQueue messages,
        IAuthUrlBuilder urls,
        IAuditLogger audit)
    {
        _accounts = accounts;
        _organizations = organizations;
        _memberships = memberships;
        _memberRoles = memberRoles;
        _operatorRoles = operatorRoles;
        _operatorRbac = operatorRbac;
        _tokens = tokens;
        _tenants = tenants;
        _tokenHasher = tokenHasher;
        _passwordHasher = passwordHasher;
        _messages = messages;
        _urls = urls;
        _audit = audit;
    }

    public async Task InviteAsync(Guid organizationId, Guid projectIdForEmail, string email,
        IReadOnlyCollection<Guid> operatorRoleIds, AuditContext actor, CancellationToken ct = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalized))
            throw new InviteAccountException("An email address is required.");

        var organization = await _organizations.GetByIdAsync(organizationId, ct)
            ?? throw new InviteAccountException("Organization not found.");

        // The org's operator catalogue + system roles must exist before we can grant any role.
        await _operatorRbac.EnsureSystemRolesAsync(organizationId, ct);

        var now = DateTimeOffset.UtcNow;

        // 1) Find-or-create the global account. A brand-new invitee has no password (cannot sign in
        //    until they accept); an existing account (e.g. an employee of another org) is reused.
        var account = await _accounts.GetByEmailAsync(normalized, ct);
        if (account is null)
        {
            account = new AccountEntity
            {
                Email = normalized,
                PasswordHash = null,
                EmailVerified = false,
                Status = AccountStatus.Active,
                CreatedAt = now
            };
            await _accounts.AddAsync(account, ct);
            await _audit.LogAsync("account.created", actor,
                resourceType: "account", resourceId: account.Id, metadata: new { account.Email }, ct: ct);
        }

        // 2) Create or reuse the membership. Re-inviting an already-active member is rejected.
        var membership = await _memberships.GetAsync(account.Id, organizationId, ct);
        if (membership is null)
        {
            membership = new OrganizationMembership
            {
                AccountId = account.Id,
                OrganizationId = organizationId,
                Status = MembershipStatus.Invited,
                InvitedByAccountId = actor.ActorId,
                CreatedAt = now
            };
            await _memberships.AddAsync(membership, ct);
        }
        else if (membership.Status == MembershipStatus.Active)
        {
            throw new InviteAccountException("That person is already a member of this organization.");
        }

        // 3) Grant the selected operator roles (validated against the org's catalogue; idempotent).
        var validRoleIds = (await _operatorRoles.ListRolesAsync(organizationId, ct)).Select(r => r.Id).ToHashSet();
        foreach (var roleId in operatorRoleIds.Where(validRoleIds.Contains).Distinct())
        {
            await _memberRoles.AssignAsync(new MemberRole
            {
                OrganizationMembershipId = membership.Id,
                OperatorRoleId = roleId,
                OrganizationId = organizationId,
                GrantedByAccountId = actor.ActorId,
                GrantedAt = now
            }, ct);
        }

        // 4) Single-use token (supersedes any outstanding one for this account+org).
        await _tokens.InvalidateOutstandingAsync(account.Id, organizationId, ct);
        var raw = _tokenHasher.GenerateRawToken();
        await _tokens.AddAsync(new AccountInviteToken
        {
            AccountId = account.Id,
            OrganizationId = organizationId,
            TokenHash = _tokenHasher.Hash(raw),
            ExpiresAt = now.Add(InviteLifetime),
            CreatedAt = now
        }, ct);

        // 5) Email the accept link through the org's active project provider (tenant-less link).
        var url = _urls.BuildInviteAcceptUrl(raw);
        _messages.Enqueue(new MessageSendRequest(projectIdForEmail, MessageTemplateKeys.OperatorInvite,
            MessageChannel.Email, account.Email, new Dictionary<string, string>
            {
                ["user_name"] = NameOf(account),
                ["org_name"] = organization.Name,
                ["action_url"] = url,
                ["expiry_hours"] = ((int)InviteLifetime.TotalHours).ToString()
            }));

        await _audit.LogAsync("member.invited", actor,
            resourceType: "organization_membership", resourceId: membership.Id,
            metadata: new { account.Email, organizationId, roles = operatorRoleIds.Count }, ct: ct);
    }

    public async Task<AccountInviteToken?> FindPendingAsync(string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;
        var token = await _tokens.GetByHashAsync(_tokenHasher.Hash(rawToken), ct);
        if (token is null || token.Used || token.ExpiresAt <= DateTimeOffset.UtcNow) return null;
        return token;
    }

    public async Task<AcceptInviteResult?> AcceptAsync(string rawToken, string newPassword, RequestInfo info, CancellationToken ct = default)
    {
        var token = await FindPendingAsync(rawToken, ct);
        if (token is null) return null;

        var account = await _accounts.GetByIdAsync(token.AccountId, ct);
        var membership = await _memberships.GetAsync(token.AccountId, token.OrganizationId, ct);
        if (account is null || membership is null) return null;

        // The org must still have a project to land the operator in.
        var project = (await _tenants.ListByOrganizationAsync(token.OrganizationId, ct)).FirstOrDefault();
        if (project is null) return null;

        // Consume the token first so a replay can't re-run the flow.
        token.Used = true;
        await _tokens.UpdateAsync(token, ct);

        // A pending (password-less) invitee sets their password here; an existing account keeps its own.
        if (string.IsNullOrEmpty(account.PasswordHash))
        {
            if (string.IsNullOrWhiteSpace(newPassword))
                throw new InviteAccountException("A password is required to set up your account.");
            account.PasswordHash = _passwordHasher.Hash(newPassword);
        }
        account.EmailVerified = true;
        await _accounts.UpdateAsync(account, ct);

        if (membership.Status != MembershipStatus.Active)
        {
            membership.Status = MembershipStatus.Active;
            await _memberships.UpdateAsync(membership, ct);
        }

        var actor = new AuditContext(account.Id, "account", info.IpAddress, info.UserAgent);
        await _audit.LogAsync("member.invite_accepted", actor,
            resourceType: "organization_membership", resourceId: membership.Id,
            metadata: new { account.Email, token.OrganizationId }, ct: ct);

        return new AcceptInviteResult(account, token.OrganizationId, project.Id);
    }

    private static string NameOf(AccountEntity account)
    {
        var name = $"{account.FirstName} {account.LastName}".Trim();
        return string.IsNullOrEmpty(name) ? account.Email : name;
    }
}
