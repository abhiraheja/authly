using System.Text.Json;
using System.Text.Json.Serialization;

namespace Authly.Core.Policies;

/// <summary>
/// Which end-users a policy applies to. Phase 1 supports a single audience dimension; the shape is
/// forward-compatible with the Phase 3 AND/OR rule-groups. Serialized to the
/// <c>policies.targeting</c> jsonb column via <see cref="PolicyTargetingJson"/>.
/// </summary>
public sealed class PolicyTargeting
{
    /// <summary>One of <see cref="Audiences"/>: all | applications | authMethods | providers.</summary>
    public string Audience { get; set; } = Audiences.All;

    /// <summary>OAuth application ids the policy targets (when <see cref="Audience"/> = applications).</summary>
    public List<Guid> ApplicationIds { get; set; } = new();

    /// <summary>Auth methods the policy targets, e.g. "password", "social" (when Audience = authMethods).</summary>
    public List<string> AuthMethods { get; set; } = new();

    /// <summary>Social provider keys the policy targets, e.g. "google" (when Audience = providers).</summary>
    public List<string> Providers { get; set; } = new();

    /// <summary>End-user role names the policy targets (when Audience = roles, or in advanced mode).</summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>For <see cref="Audiences.Advanced"/>: how the populated dimensions combine — "any" (OR) or "all" (AND).</summary>
    public string Match { get; set; } = "any";

    public static PolicyTargeting All() => new() { Audience = Audiences.All };

    /// <summary>True if this targeting (in its current audience) makes use of the application dimension.</summary>
    public bool UsesApplications => Audience == Audiences.Applications || (Audience == Audiences.Advanced && ApplicationIds.Count > 0);
    public bool UsesAuthMethods => Audience == Audiences.AuthMethods || (Audience == Audiences.Advanced && AuthMethods.Count > 0);
    public bool UsesProviders => Audience == Audiences.Providers || (Audience == Audiences.Advanced && Providers.Count > 0);
    public bool UsesRoles => Audience == Audiences.Roles || (Audience == Audiences.Advanced && Roles.Count > 0);
}

/// <summary>The supported audience kinds for <see cref="PolicyTargeting.Audience"/>.</summary>
public static class Audiences
{
    public const string All = "all";
    public const string Applications = "applications";
    public const string AuthMethods = "authMethods";
    public const string Providers = "providers";
    public const string Roles = "roles";
    /// <summary>Combine multiple dimensions (apps/auth-methods/providers/roles) with an any/all match.</summary>
    public const string Advanced = "advanced";
}

/// <summary>Parse/serialize helpers for the <c>policies.targeting</c> jsonb column.</summary>
public static class PolicyTargetingJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static PolicyTargeting Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return PolicyTargeting.All();
        try
        {
            return JsonSerializer.Deserialize<PolicyTargeting>(json, Options) ?? PolicyTargeting.All();
        }
        catch (JsonException)
        {
            return PolicyTargeting.All();
        }
    }

    public static string Serialize(PolicyTargeting targeting)
        => JsonSerializer.Serialize(targeting, Options);
}
