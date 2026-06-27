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

    public static PolicyTargeting All() => new() { Audience = Audiences.All };
}

/// <summary>The supported audience kinds for <see cref="PolicyTargeting.Audience"/>.</summary>
public static class Audiences
{
    public const string All = "all";
    public const string Applications = "applications";
    public const string AuthMethods = "authMethods";
    public const string Providers = "providers";
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
