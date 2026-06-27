using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Policies;
using Authly.Modules.Policies;
using Xunit;

namespace Authly.Tests.Policies;

public sealed class UserPromptServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private static readonly Guid User = Guid.NewGuid();

    private readonly FakePolicyRepo _policies = new();
    private readonly FakeLoginHistoryRepo _logins = new();
    private readonly FakeSocialIdentityRepo _social = new();

    private UserPromptService Sut() => new(_policies, _logins, _social, new NoopAudit());

    /// <summary>Creates a published policy + its live version, returns the policy.</summary>
    private Policy Publish(PolicyEnforcementMode mode, PolicyTargeting? targeting = null,
        DateTimeOffset? skipDeadline = null, DateTimeOffset? startsAt = null, DateTimeOffset? closeDate = null)
    {
        var policy = new Policy
        {
            Id = Guid.NewGuid(),
            TenantId = Tenant,
            Title = "Terms",
            Status = PolicyStatus.Published,
            EnforcementMode = mode,
            SkipDeadline = skipDeadline,
            StartsAt = startsAt,
            CloseDate = closeDate,
            Targeting = PolicyTargetingJson.Serialize(targeting ?? PolicyTargeting.All())
        };
        var version = new PolicyVersion
        {
            Id = Guid.NewGuid(), PolicyId = policy.Id, TenantId = Tenant, Version = 1,
            ContentType = PolicyContentType.Html, HtmlContent = "<p>terms</p>", PublishedAt = DateTimeOffset.UtcNow
        };
        policy.CurrentVersionId = version.Id;
        _policies.Policies.Add(policy);
        _policies.Versions.Add(version);
        return policy;
    }

    private void Decide(Policy p, PolicyDecisionType decision, Guid? sessionId = null, Guid? versionId = null, DateTimeOffset? at = null)
        => _policies.Decisions.Add(new PolicyDecision
        {
            TenantId = Tenant, UserId = User, PolicyId = p.Id,
            PolicyVersionId = versionId ?? p.CurrentVersionId!.Value,
            Decision = decision, SessionId = sessionId, DecidedAt = at ?? DateTimeOffset.UtcNow
        });

    [Fact]
    public async Task No_policies_means_no_prompts()
    {
        var result = await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null);
        Assert.False(result.Any);
    }

    [Fact]
    public async Task Mandatory_unaccepted_blocks()
    {
        Publish(PolicyEnforcementMode.Mandatory);
        var result = await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null);
        var prompt = Assert.Single(result.Prompts);
        Assert.True(prompt.HardBlock);
        Assert.False(prompt.AllowSkip);
    }

    [Fact]
    public async Task Mandatory_accepted_clears()
    {
        var p = Publish(PolicyEnforcementMode.Mandatory);
        Decide(p, PolicyDecisionType.Accepted);
        var result = await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null);
        Assert.False(result.Any);
    }

    [Fact]
    public async Task Skippable_skip_satisfies_only_same_session()
    {
        var session = Guid.NewGuid();
        var p = Publish(PolicyEnforcementMode.SkippableUntil, skipDeadline: DateTimeOffset.UtcNow.AddDays(7));
        Decide(p, PolicyDecisionType.Skipped, sessionId: session);

        Assert.False((await Sut().GetPendingAsync(Tenant, User, session, null)).Any);           // same session: cleared
        Assert.True((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null)).Any);      // new session: asked again
    }

    [Fact]
    public async Task Skippable_after_deadline_hard_blocks()
    {
        var p = Publish(PolicyEnforcementMode.SkippableUntil, skipDeadline: DateTimeOffset.UtcNow.AddDays(-1));
        var result = await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null);
        var prompt = Assert.Single(result.Prompts);
        Assert.True(prompt.HardBlock);
        Assert.False(prompt.AllowSkip);
    }

    [Fact]
    public async Task Optional_is_settled_by_reject_and_never_blocks()
    {
        var p = Publish(PolicyEnforcementMode.Optional);
        var before = await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null);
        Assert.True(before.Any);
        Assert.False(before.HasHardBlock);
        Assert.True(before.Prompts[0].AllowReject);

        Decide(p, PolicyDecisionType.Rejected);
        Assert.False((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null)).Any);
    }

    [Fact]
    public async Task New_version_re_prompts_after_old_acceptance()
    {
        var p = Publish(PolicyEnforcementMode.Mandatory);
        Decide(p, PolicyDecisionType.Accepted); // accepted v1
        Assert.False((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null)).Any);

        // Publish v2: new current version id; the v1 acceptance no longer satisfies.
        var v2 = new PolicyVersion { Id = Guid.NewGuid(), PolicyId = p.Id, TenantId = Tenant, Version = 2, HtmlContent = "<p>v2</p>", PublishedAt = DateTimeOffset.UtcNow };
        _policies.Versions.Add(v2);
        p.CurrentVersionId = v2.Id;

        Assert.True((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null)).Any);
    }

    [Fact]
    public async Task ConsentReset_re_prompts_even_on_same_version()
    {
        var p = Publish(PolicyEnforcementMode.Mandatory);
        Decide(p, PolicyDecisionType.Accepted, at: DateTimeOffset.UtcNow.AddMinutes(-10));
        Assert.False((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null)).Any);

        p.ConsentResetAt = DateTimeOffset.UtcNow; // admin re-requested
        Assert.True((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null)).Any);
    }

    [Fact]
    public async Task CloseDate_in_past_means_inactive()
    {
        Publish(PolicyEnforcementMode.Mandatory, closeDate: DateTimeOffset.UtcNow.AddDays(-1));
        Assert.False((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null)).Any);
    }

    [Fact]
    public async Task AuthMethod_targeting_matches_current_login_method()
    {
        Publish(PolicyEnforcementMode.Mandatory, new PolicyTargeting
        {
            Audience = Audiences.AuthMethods,
            AuthMethods = new() { AuthMethodCategories.Social }
        });

        // No login history → category unknown → no match.
        Assert.False((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null)).Any);

        // Latest login was via Google → normalizes to "social" → matches.
        _logins.Items.Add(new LoginHistory { TenantId = Tenant, UserId = User, Result = "success", Method = "google", CreatedAt = DateTimeOffset.UtcNow });
        Assert.True((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null)).Any);
    }

    [Fact]
    public async Task Application_targeting_matches_only_in_app_context()
    {
        var appId = Guid.NewGuid();
        Publish(PolicyEnforcementMode.Mandatory, new PolicyTargeting
        {
            Audience = Audiences.Applications,
            ApplicationIds = new() { appId }
        });

        Assert.False((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), null)).Any);     // portal (no app) → no match
        Assert.True((await Sut().GetPendingAsync(Tenant, User, Guid.NewGuid(), appId)).Any);     // signing into the app → match
    }
}
