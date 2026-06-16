using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Abac;
using Authly.Modules.Common;

namespace Authly.Tests.Phase2;

public class AbacTests
{
    private static AccessRequest Req(string action, string resource,
        (string, string)[]? subject = null, (string, string)[]? res = null, (string, string)[]? env = null)
        => new(action, resource,
            (subject ?? Array.Empty<(string, string)>()).ToDictionary(x => x.Item1, x => x.Item2),
            (res ?? Array.Empty<(string, string)>()).ToDictionary(x => x.Item1, x => x.Item2),
            (env ?? Array.Empty<(string, string)>()).ToDictionary(x => x.Item1, x => x.Item2));

    private static EvaluatedPolicy P(string name, PolicyEffect effect, string action, string resource,
        int priority = 0, params PolicyCondition[] conditions)
        => new(Guid.NewGuid(), name, effect, action, resource, priority, conditions);

    [Fact]
    public void No_policies_means_default_deny()
    {
        var d = AbacEngine.Evaluate(Req("document.read", "document"), Array.Empty<EvaluatedPolicy>());
        Assert.False(d.Allowed);
        Assert.Equal("default_deny", d.Reason);
    }

    [Fact]
    public void Matching_allow_permits()
    {
        var d = AbacEngine.Evaluate(Req("document.read", "document"),
            new[] { P("readers", PolicyEffect.Allow, "document.read", "document") });
        Assert.True(d.Allowed);
        Assert.Equal("readers", d.PolicyName);
    }

    [Fact]
    public void Deny_overrides_allow_regardless_of_priority()
    {
        var d = AbacEngine.Evaluate(Req("document.read", "document"), new[]
        {
            P("allow-all", PolicyEffect.Allow, "*", "*", priority: 100),
            P("block-doc", PolicyEffect.Deny, "document.read", "document", priority: 1)
        });
        Assert.False(d.Allowed);
        Assert.Equal("explicit_deny", d.Reason);
        Assert.Equal("block-doc", d.PolicyName);
    }

    [Fact]
    public void Highest_priority_allow_is_cited()
    {
        var d = AbacEngine.Evaluate(Req("document.read", "document"), new[]
        {
            P("low", PolicyEffect.Allow, "*", "*", priority: 1),
            P("high", PolicyEffect.Allow, "document.*", "document", priority: 50)
        });
        Assert.True(d.Allowed);
        Assert.Equal("high", d.PolicyName);
    }

    [Fact]
    public void Glob_action_and_resource_matching()
    {
        var pol = new[] { P("docs", PolicyEffect.Allow, "document.*", "doc*") };
        Assert.True(AbacEngine.Evaluate(Req("document.write", "document"), pol).Allowed);
        Assert.True(AbacEngine.Evaluate(Req("document.read", "documents"), pol).Allowed);
        Assert.False(AbacEngine.Evaluate(Req("billing.read", "document"), pol).Allowed); // action doesn't match
    }

    [Fact]
    public void Conditions_must_all_hold()
    {
        var pol = new[] { P("eng-only", PolicyEffect.Allow, "*", "*", 0,
            new PolicyCondition("subject.department", ConditionOperator.Equals, "eng"),
            new PolicyCondition("subject.level", ConditionOperator.GreaterThan, "3")) };

        Assert.True(AbacEngine.Evaluate(Req("a", "b", subject: new[] { ("department", "eng"), ("level", "5") }), pol).Allowed);
        Assert.False(AbacEngine.Evaluate(Req("a", "b", subject: new[] { ("department", "eng"), ("level", "2") }), pol).Allowed); // level fails
        Assert.False(AbacEngine.Evaluate(Req("a", "b", subject: new[] { ("department", "sales"), ("level", "9") }), pol).Allowed); // dept fails
    }

    [Theory]
    [InlineData(ConditionOperator.NotEquals, "sales", "eng", true)]
    [InlineData(ConditionOperator.NotEquals, "eng", "eng", false)]
    [InlineData(ConditionOperator.Contains, "engineering", "gin", true)]
    [InlineData(ConditionOperator.In, "eng", "sales,eng,ops", true)]
    [InlineData(ConditionOperator.In, "hr", "sales,eng,ops", false)]
    public void Operator_semantics(ConditionOperator op, string actual, string expected, bool shouldAllow)
    {
        var pol = new[] { P("p", PolicyEffect.Allow, "*", "*", 0,
            new PolicyCondition("subject.dept", op, expected)) };
        var d = AbacEngine.Evaluate(Req("a", "b", subject: new[] { ("dept", actual) }), pol);
        Assert.Equal(shouldAllow, d.Allowed);
    }

    [Fact]
    public void Exists_checks_presence_only()
    {
        var pol = new[] { P("has-mfa", PolicyEffect.Allow, "*", "*", 0,
            new PolicyCondition("subject.mfa", ConditionOperator.Exists, null)) };
        Assert.True(AbacEngine.Evaluate(Req("a", "b", subject: new[] { ("mfa", "totp") }), pol).Allowed);
        Assert.False(AbacEngine.Evaluate(Req("a", "b"), pol).Allowed);
    }

    // --- Decision service over the repository ---

    [Fact]
    public async Task DecisionService_only_evaluates_enabled_policies_and_parses_conditions()
    {
        var repo = new FakeAccessPolicyRepo();
        repo.Items.Add(new AccessPolicy
        {
            Id = Guid.NewGuid(), TenantId = Guid.Empty, Name = "eng-allow", Effect = "allow",
            Action = "document.*", ResourceType = "document", Enabled = true, Priority = 10,
            Conditions = """[{"attribute":"subject.department","operator":"equals","value":"eng"}]"""
        });
        var svc = new AuthorizationDecisionService(repo);

        var empty = new Dictionary<string, string>();
        var allow = await svc.EvaluateAsync(Guid.Empty,
            new AccessRequest("document.read", "document",
                new Dictionary<string, string> { ["department"] = "eng" }, empty, empty));
        Assert.True(allow.Allowed);

        var deny = await svc.EvaluateAsync(Guid.Empty,
            new AccessRequest("document.read", "document",
                new Dictionary<string, string> { ["department"] = "sales" }, empty, empty));
        Assert.False(deny.Allowed);
    }
}

internal sealed class FakeAccessPolicyRepo : IAccessPolicyRepository
{
    public readonly List<AccessPolicy> Items = new();
    public Task<IReadOnlyList<AccessPolicy>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AccessPolicy>>(Items.Where(p => p.TenantId == tenantId).ToList());
    public Task<IReadOnlyList<AccessPolicy>> ListEnabledAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<AccessPolicy>>(Items.Where(p => p.TenantId == tenantId && p.Enabled).ToList());
    public Task<AccessPolicy?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == tenantId && p.Id == id));
    public Task AddAsync(AccessPolicy policy, CancellationToken ct = default) { Items.Add(policy); return Task.CompletedTask; }
    public Task UpdateAsync(AccessPolicy policy, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(AccessPolicy policy, CancellationToken ct = default) { Items.Remove(policy); return Task.CompletedTask; }
}
