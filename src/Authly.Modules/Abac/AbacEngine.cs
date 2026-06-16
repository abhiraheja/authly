using System.Globalization;

namespace Authly.Modules.Abac;

public enum PolicyEffect { Allow, Deny }

public enum ConditionOperator { Equals, NotEquals, Contains, In, GreaterThan, LessThan, Exists }

/// <summary>One attribute test, e.g. <c>subject.department Equals "eng"</c>.</summary>
public sealed record PolicyCondition(string Attribute, ConditionOperator Operator, string? Value);

/// <summary>A policy reduced to what the engine needs (conditions already parsed).</summary>
public sealed record EvaluatedPolicy(
    Guid Id, string Name, PolicyEffect Effect, string Action, string ResourceType,
    int Priority, IReadOnlyList<PolicyCondition> Conditions);

/// <summary>The decision request: an action on a resource type, with attribute bags.</summary>
public sealed record AccessRequest(
    string Action,
    string ResourceType,
    IReadOnlyDictionary<string, string> Subject,
    IReadOnlyDictionary<string, string> Resource,
    IReadOnlyDictionary<string, string> Environment);

/// <summary>The decision: allow/deny, the deciding policy (if any), and a machine reason.</summary>
public sealed record AccessDecision(bool Allowed, string? PolicyName, string Reason);

/// <summary>
/// Pure ABAC policy decision point. No I/O — callers load and parse policies, then evaluate.
/// Combining rule: <b>deny-overrides</b>. Among policies matching the action/resource whose
/// conditions all hold, any Deny denies; otherwise the highest-priority Allow permits; otherwise
/// the default is deny.
/// </summary>
public static class AbacEngine
{
    public static AccessDecision Evaluate(AccessRequest request, IEnumerable<EvaluatedPolicy> policies)
    {
        var matching = policies
            .Where(p => GlobMatches(p.Action, request.Action) && GlobMatches(p.ResourceType, request.ResourceType))
            .Where(p => p.Conditions.All(c => ConditionHolds(c, request)))
            .ToList();

        var deny = matching.Where(p => p.Effect == PolicyEffect.Deny)
            .OrderByDescending(p => p.Priority).FirstOrDefault();
        if (deny is not null)
            return new AccessDecision(false, deny.Name, "explicit_deny");

        var allow = matching.Where(p => p.Effect == PolicyEffect.Allow)
            .OrderByDescending(p => p.Priority).FirstOrDefault();
        if (allow is not null)
            return new AccessDecision(true, allow.Name, "allow");

        return new AccessDecision(false, null, "default_deny");
    }

    /// <summary>Pattern matching: "*" matches anything, "prefix*" matches by prefix, else exact (case-insensitive).</summary>
    private static bool GlobMatches(string pattern, string value)
    {
        if (pattern == "*") return true;
        if (pattern.EndsWith('*'))
            return value.StartsWith(pattern[..^1], StringComparison.OrdinalIgnoreCase);
        return string.Equals(pattern, value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ConditionHolds(PolicyCondition c, AccessRequest req)
    {
        var present = TryResolve(c.Attribute, req, out var actual);
        return c.Operator switch
        {
            ConditionOperator.Exists => present,
            ConditionOperator.Equals => present && string.Equals(actual, c.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.NotEquals => !present || !string.Equals(actual, c.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Contains => present && actual!.Contains(c.Value ?? "", StringComparison.OrdinalIgnoreCase),
            ConditionOperator.In => present && (c.Value ?? "")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(v => string.Equals(v, actual, StringComparison.OrdinalIgnoreCase)),
            ConditionOperator.GreaterThan => Numeric(actual, present) is { } a && Numeric(c.Value, true) is { } b && a > b,
            ConditionOperator.LessThan => Numeric(actual, present) is { } a && Numeric(c.Value, true) is { } b && a < b,
            _ => false
        };
    }

    // Attribute path: "subject.x", "resource.x", "environment.x" (alias "env.x").
    private static bool TryResolve(string attribute, AccessRequest req, out string? value)
    {
        value = null;
        var dot = attribute.IndexOf('.');
        if (dot <= 0) return false;
        var scope = attribute[..dot];
        var key = attribute[(dot + 1)..];
        var bag = scope.ToLowerInvariant() switch
        {
            "subject" => req.Subject,
            "resource" => req.Resource,
            "environment" or "env" => req.Environment,
            _ => null
        };
        return bag is not null && bag.TryGetValue(key, out value);
    }

    private static double? Numeric(string? s, bool present)
        => present && double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : null;
}
