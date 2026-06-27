using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Policies;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Policies;

/// <summary>
/// The enforcement "brain": given the current sign-in context, returns the policies a user must be
/// shown before proceeding. Used by the sign-in gate middleware and the consent page. Evaluates
/// published+active policies against targeting (audience / application / auth-method / provider),
/// the user's prior decisions (per current version, honoring a consent-reset), and per-session skips.
/// </summary>
public interface IUserPromptService
{
    Task<PendingPromptSet> GetPendingAsync(Guid tenantId, Guid userId, Guid? sessionId,
        Guid? currentApplicationId, CancellationToken ct = default);

    /// <summary>Records a user's decision on a policy's live version (accept/reject/skip).</summary>
    Task RecordDecisionAsync(Guid tenantId, Guid userId, Guid policyId, PolicyDecisionType decision,
        Guid? sessionId, Guid? applicationId, AuditContext actor, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class UserPromptService : IUserPromptService
{
    private readonly IPolicyRepository _policies;
    private readonly ILoginHistoryRepository _loginHistory;
    private readonly ISocialIdentityRepository _socialIdentities;
    private readonly IUserRoleRepository _userRoles;
    private readonly IAuditLogger _audit;

    public UserPromptService(IPolicyRepository policies, ILoginHistoryRepository loginHistory,
        ISocialIdentityRepository socialIdentities, IUserRoleRepository userRoles, IAuditLogger audit)
    {
        _policies = policies;
        _loginHistory = loginHistory;
        _socialIdentities = socialIdentities;
        _userRoles = userRoles;
        _audit = audit;
    }

    public async Task RecordDecisionAsync(Guid tenantId, Guid userId, Guid policyId, PolicyDecisionType decision,
        Guid? sessionId, Guid? applicationId, AuditContext actor, CancellationToken ct = default)
    {
        var policy = await _policies.GetAsync(tenantId, policyId, ct);
        if (policy?.CurrentVersionId is not { } versionId)
            throw new InvalidOperationException($"Policy {policyId} has no live version to decide on.");

        var version = await _policies.GetVersionAsync(tenantId, versionId, ct);

        await _policies.AddDecisionAsync(new PolicyDecision
        {
            TenantId = tenantId,
            UserId = userId,
            PolicyId = policyId,
            PolicyVersionId = versionId,
            Version = version?.Version ?? 0,
            Decision = decision,
            SessionId = sessionId,
            ApplicationId = applicationId,
            IpAddress = actor.IpAddress,
            UserAgent = actor.UserAgent,
            DecidedAt = DateTimeOffset.UtcNow
        }, ct);

        await _audit.LogAsync("policy.decision_recorded", actor, tenantId: tenantId,
            resourceType: "policy", resourceId: policyId,
            metadata: new { decision = decision.ToString(), version = version?.Version ?? 0 }, ct: ct);
    }

    public async Task<PendingPromptSet> GetPendingAsync(Guid tenantId, Guid userId, Guid? sessionId,
        Guid? currentApplicationId, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var active = (await _policies.ListPublishedAsync(tenantId, ct))
            .Where(p => p.CurrentVersionId is not null)
            .Where(p => p.StartsAt is null || now >= p.StartsAt)
            .Where(p => p.CloseDate is null || now < p.CloseDate)
            .ToList();
        if (active.Count == 0) return PendingPromptSet.Empty;

        // Parse targeting once; lazily load the signals only if some policy needs them.
        var targetings = active.ToDictionary(p => p.Id, p => PolicyTargetingJson.Parse(p.Targeting));

        string? authCategory = null;
        if (targetings.Values.Any(t => t.UsesAuthMethods))
            authCategory = await TargetingEvaluator.AuthCategoryAsync(_loginHistory, tenantId, userId, ct);

        HashSet<string>? linkedProviders = null;
        if (targetings.Values.Any(t => t.UsesProviders))
            linkedProviders = await TargetingEvaluator.LinkedProvidersAsync(_socialIdentities, tenantId, userId, ct);

        HashSet<string>? userRoles = null;
        if (targetings.Values.Any(t => t.UsesRoles))
            userRoles = await TargetingEvaluator.UserRolesAsync(_userRoles, tenantId, userId, ct);

        var decisions = await _policies.ListDecisionsForUserAsync(tenantId, userId, ct);

        var prompts = new List<PendingPrompt>();
        foreach (var policy in active)
        {
            if (!TargetingEvaluator.Matches(targetings[policy.Id], currentApplicationId, authCategory, linkedProviders, userRoles))
                continue;

            // Decisions that still count: for the live version, made after any consent-reset cutoff.
            var relevant = decisions.Where(d =>
                d.PolicyId == policy.Id &&
                d.PolicyVersionId == policy.CurrentVersionId &&
                (policy.ConsentResetAt is null || d.DecidedAt > policy.ConsentResetAt)).ToList();

            if (IsPermanentlySatisfied(policy.EnforcementMode, relevant)) continue;

            // A skip only satisfies its own session.
            var skippedThisSession = sessionId is { } sid &&
                relevant.Any(d => d.Decision == PolicyDecisionType.Skipped && d.SessionId == sid);
            if (skippedThisSession && policy.EnforcementMode != PolicyEnforcementMode.Mandatory)
            {
                // After the skip deadline a SkippableUntil policy can no longer be deferred.
                var pastDeadline = policy.EnforcementMode == PolicyEnforcementMode.SkippableUntil
                    && policy.SkipDeadline is { } dl && now >= dl;
                if (!pastDeadline) continue;
            }

            var version = await _policies.GetVersionAsync(tenantId, policy.CurrentVersionId!.Value, ct);
            if (version is null) continue;

            var (hardBlock, allowSkip, allowReject) = Flags(policy, now);
            prompts.Add(new PendingPrompt(
                policy.Id, version.Id, version.Version, policy.Title, policy.Description,
                version.ContentType, version.HtmlContent, version.AssetId,
                policy.EnforcementMode, hardBlock, allowSkip, allowReject));
        }

        return new PendingPromptSet(prompts);
    }

    private static bool IsPermanentlySatisfied(PolicyEnforcementMode mode, List<PolicyDecision> relevant) => mode switch
    {
        // Optional is settled by any opinion (accept or reject); accept settles the rest.
        PolicyEnforcementMode.Optional => relevant.Any(d => d.Decision is PolicyDecisionType.Accepted or PolicyDecisionType.Rejected),
        _ => relevant.Any(d => d.Decision == PolicyDecisionType.Accepted)
    };

    private static (bool HardBlock, bool AllowSkip, bool AllowReject) Flags(Policy policy, DateTimeOffset now) =>
        policy.EnforcementMode switch
        {
            PolicyEnforcementMode.Mandatory => (true, false, false),
            PolicyEnforcementMode.Optional => (false, true, true),
            // SkippableUntil: blocks once the deadline passes; before that, skip is allowed.
            PolicyEnforcementMode.SkippableUntil when policy.SkipDeadline is { } dl && now >= dl => (true, false, false),
            PolicyEnforcementMode.SkippableUntil => (false, true, false),
            _ => (true, false, false)
        };

}
