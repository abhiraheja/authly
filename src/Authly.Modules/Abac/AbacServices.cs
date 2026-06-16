using System.Text.Json;
using System.Text.Json.Serialization;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Abac;

/// <summary>Editable fields for an access policy.</summary>
public sealed record AccessPolicyInput(
    string Name, string? Description, PolicyEffect Effect, string Action, string ResourceType,
    IReadOnlyList<PolicyCondition> Conditions, int Priority, bool Enabled);

public sealed class AccessPolicyInvalidException(string message) : Exception(message);

/// <summary>Shared JSON settings for the conditions column (enums as strings, camelCase).</summary>
public static class AbacJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    /// <summary>Lenient parse (used for stored conditions): malformed JSON yields no conditions.</summary>
    public static IReadOnlyList<PolicyCondition> ParseConditions(string json)
    {
        try { return JsonSerializer.Deserialize<List<PolicyCondition>>(json, Options) ?? new(); }
        catch (JsonException) { return new List<PolicyCondition>(); }
    }

    /// <summary>Strict parse for user input: throws <see cref="AccessPolicyInvalidException"/> on bad JSON.</summary>
    public static IReadOnlyList<PolicyCondition> ParseConditionsStrict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<PolicyCondition>();
        try { return JsonSerializer.Deserialize<List<PolicyCondition>>(json, Options) ?? new(); }
        catch (JsonException ex) { throw new AccessPolicyInvalidException($"Conditions JSON is invalid: {ex.Message}"); }
    }

    public static string Serialize(IReadOnlyList<PolicyCondition> conditions)
        => JsonSerializer.Serialize(conditions, Options);
}

/// <summary>Tenant-admin CRUD for ABAC policies.</summary>
public interface IAccessPolicyService
{
    Task<IReadOnlyList<AccessPolicy>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<AccessPolicy?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<AccessPolicy> CreateAsync(Guid tenantId, AccessPolicyInput input, AuditContext actor, CancellationToken ct = default);
    Task UpdateAsync(Guid tenantId, Guid id, AccessPolicyInput input, AuditContext actor, CancellationToken ct = default);
    Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);
}

/// <summary>The policy decision point: evaluates a request against the tenant's enabled policies.</summary>
public interface IAuthorizationDecisionService
{
    Task<AccessDecision> EvaluateAsync(Guid tenantId, AccessRequest request, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class AccessPolicyService : IAccessPolicyService
{
    private readonly IAccessPolicyRepository _repo;
    private readonly IAuditLogger _audit;

    public AccessPolicyService(IAccessPolicyRepository repo, IAuditLogger audit)
    {
        _repo = repo;
        _audit = audit;
    }

    public Task<IReadOnlyList<AccessPolicy>> ListAsync(Guid tenantId, CancellationToken ct = default) => _repo.ListByTenantAsync(tenantId, ct);
    public Task<AccessPolicy?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default) => _repo.GetByIdAsync(tenantId, id, ct);

    public async Task<AccessPolicy> CreateAsync(Guid tenantId, AccessPolicyInput input, AuditContext actor, CancellationToken ct = default)
    {
        var policy = new AccessPolicy
        {
            TenantId = tenantId,
            CreatedAt = DateTimeOffset.UtcNow
        };
        Apply(policy, input);
        await _repo.AddAsync(policy, ct);
        await _audit.LogAsync("access_policy.created", actor, tenantId, "access_policy", policy.Id,
            metadata: new { policy.Name, policy.Effect, policy.Action, policy.ResourceType }, ct: ct);
        return policy;
    }

    public async Task UpdateAsync(Guid tenantId, Guid id, AccessPolicyInput input, AuditContext actor, CancellationToken ct = default)
    {
        var policy = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Access policy {id} not found.");
        Apply(policy, input);
        await _repo.UpdateAsync(policy, ct);
        await _audit.LogAsync("access_policy.updated", actor, tenantId, "access_policy", id, ct: ct);
    }

    public async Task DeleteAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var policy = await _repo.GetByIdAsync(tenantId, id, ct)
            ?? throw new KeyNotFoundException($"Access policy {id} not found.");
        await _repo.DeleteAsync(policy, ct);
        await _audit.LogAsync("access_policy.deleted", actor, tenantId, "access_policy", id, ct: ct);
    }

    private static void Apply(AccessPolicy policy, AccessPolicyInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) throw new AccessPolicyInvalidException("Name is required.");
        if (string.IsNullOrWhiteSpace(input.Action)) throw new AccessPolicyInvalidException("Action is required.");
        if (string.IsNullOrWhiteSpace(input.ResourceType)) throw new AccessPolicyInvalidException("Resource type is required.");

        policy.Name = input.Name.Trim();
        policy.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        policy.Effect = input.Effect == PolicyEffect.Deny ? "deny" : "allow";
        policy.Action = input.Action.Trim();
        policy.ResourceType = input.ResourceType.Trim();
        policy.Priority = input.Priority;
        policy.Enabled = input.Enabled;
        policy.Conditions = JsonSerializer.Serialize(input.Conditions ?? Array.Empty<PolicyCondition>(), AbacJson.Options);
    }
}

/// <inheritdoc />
public sealed class AuthorizationDecisionService : IAuthorizationDecisionService
{
    private readonly IAccessPolicyRepository _repo;

    public AuthorizationDecisionService(IAccessPolicyRepository repo) => _repo = repo;

    public async Task<AccessDecision> EvaluateAsync(Guid tenantId, AccessRequest request, CancellationToken ct = default)
    {
        var policies = await _repo.ListEnabledAsync(tenantId, ct);
        var evaluated = policies.Select(p => new EvaluatedPolicy(
            p.Id, p.Name,
            string.Equals(p.Effect, "deny", StringComparison.OrdinalIgnoreCase) ? PolicyEffect.Deny : PolicyEffect.Allow,
            p.Action, p.ResourceType, p.Priority, AbacJson.ParseConditions(p.Conditions)));
        return AbacEngine.Evaluate(request, evaluated);
    }
}
