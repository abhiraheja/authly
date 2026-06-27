using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Policies;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Policies;

/// <summary>Admin-facing management of the policies/consent engine (drives the TenantAdmin UI).</summary>
public interface IPolicyService
{
    Task<IReadOnlyList<Policy>> ListAsync(Guid tenantId, CancellationToken ct = default);
    Task<Policy?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyVersion>> ListVersionsAsync(Guid tenantId, Guid policyId, CancellationToken ct = default);
    Task<IReadOnlyList<PolicyDecision>> ListDecisionsAsync(Guid tenantId, Guid policyId, CancellationToken ct = default);

    /// <summary>A user's own decision history (for the portal "policies" page).</summary>
    Task<IReadOnlyList<PolicyDecision>> ListUserDecisionsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    Task<Policy> CreateAsync(Guid tenantId, PolicyEditInput input, AuditContext actor, CancellationToken ct = default);
    Task UpdateAsync(Guid tenantId, Guid id, PolicyEditInput input, AuditContext actor, CancellationToken ct = default);

    /// <summary>Stores an uploaded PDF as the policy's draft content.</summary>
    Task UploadPdfAsync(Guid tenantId, Guid id, byte[] data, string contentType, string fileName, AuditContext actor, CancellationToken ct = default);

    /// <summary>Snapshots the draft content into a new immutable version and makes the policy live.</summary>
    Task PublishAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    Task ArchiveAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    /// <summary>Re-requests acceptance from everyone (sets the consent-reset cutoff to now); the prior
    /// audit trail is preserved but everyone is prompted again on next sign-in.</summary>
    Task ReRequestAcceptanceAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default);

    /// <summary>Fetches an uploaded PDF (by unguessable id) for serving/preview.</summary>
    Task<PolicyAsset?> GetAssetAsync(Guid id, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class PolicyService : IPolicyService
{
    /// <summary>Accepted upload types for a policy document.</summary>
    private static readonly HashSet<string> AllowedDocTypes = new(StringComparer.OrdinalIgnoreCase) { "application/pdf" };

    private readonly IPolicyRepository _policies;
    private readonly IAuditLogger _audit;

    public PolicyService(IPolicyRepository policies, IAuditLogger audit)
    {
        _policies = policies;
        _audit = audit;
    }

    public Task<IReadOnlyList<Policy>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => _policies.ListByTenantAsync(tenantId, ct);

    public Task<Policy?> GetAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => _policies.GetAsync(tenantId, id, ct);

    public Task<IReadOnlyList<PolicyVersion>> ListVersionsAsync(Guid tenantId, Guid policyId, CancellationToken ct = default)
        => _policies.ListVersionsAsync(policyId, ct);

    public Task<IReadOnlyList<PolicyDecision>> ListDecisionsAsync(Guid tenantId, Guid policyId, CancellationToken ct = default)
        => _policies.ListDecisionsForPolicyAsync(tenantId, policyId, ct);

    public Task<IReadOnlyList<PolicyDecision>> ListUserDecisionsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _policies.ListDecisionsForUserAsync(tenantId, userId, ct);

    public async Task<Policy> CreateAsync(Guid tenantId, PolicyEditInput input, AuditContext actor, CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var policy = new Policy
        {
            TenantId = tenantId,
            Title = input.Title.Trim(),
            Description = Normalize(input.Description),
            Status = PolicyStatus.Draft,
            EnforcementMode = input.EnforcementMode,
            SkipDeadline = input.SkipDeadline,
            StartsAt = input.StartsAt,
            CloseDate = input.CloseDate,
            Targeting = PolicyTargetingJson.Serialize(input.Targeting),
            DraftContentType = input.ContentType,
            DraftHtmlContent = input.ContentType == PolicyContentType.Html ? PolicyHtmlSanitizer.Sanitize(input.HtmlContent) : null,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _policies.AddAsync(policy, ct);
        await _audit.LogAsync("policy.created", actor, tenantId: tenantId,
            resourceType: "policy", resourceId: policy.Id, metadata: new { policy.Title }, ct: ct);
        return policy;
    }

    public async Task UpdateAsync(Guid tenantId, Guid id, PolicyEditInput input, AuditContext actor, CancellationToken ct = default)
    {
        var policy = await Require(tenantId, id, ct);
        policy.Title = input.Title.Trim();
        policy.Description = Normalize(input.Description);
        policy.EnforcementMode = input.EnforcementMode;
        policy.SkipDeadline = input.SkipDeadline;
        policy.StartsAt = input.StartsAt;
        policy.CloseDate = input.CloseDate;
        policy.Targeting = PolicyTargetingJson.Serialize(input.Targeting);

        // Switching to HTML replaces the draft body; switching to PDF keeps the previously uploaded asset.
        policy.DraftContentType = input.ContentType;
        if (input.ContentType == PolicyContentType.Html)
            policy.DraftHtmlContent = PolicyHtmlSanitizer.Sanitize(input.HtmlContent);

        policy.UpdatedAt = DateTimeOffset.UtcNow;
        await _policies.UpdateAsync(policy, ct);
        await _audit.LogAsync("policy.updated", actor, tenantId: tenantId,
            resourceType: "policy", resourceId: policy.Id, metadata: new { policy.Title }, ct: ct);
    }

    public async Task UploadPdfAsync(Guid tenantId, Guid id, byte[] data, string contentType, string fileName, AuditContext actor, CancellationToken ct = default)
    {
        var policy = await Require(tenantId, id, ct);

        if (data is null || data.Length == 0)
            throw new PolicyInvalidException("The uploaded file is empty.");
        var type = contentType?.Trim().ToLowerInvariant() ?? "";
        if (!AllowedDocTypes.Contains(type))
            throw new PolicyInvalidException("Upload a PDF document.");

        var asset = new PolicyAsset
        {
            TenantId = tenantId,
            PolicyId = policy.Id,
            FileName = string.IsNullOrWhiteSpace(fileName) ? "policy.pdf" : Path.GetFileName(fileName),
            ContentType = type,
            Data = data,
            SizeBytes = data.Length,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _policies.AddAssetAsync(asset, ct);

        policy.DraftContentType = PolicyContentType.Pdf;
        policy.DraftAssetId = asset.Id;
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        await _policies.UpdateAsync(policy, ct);

        await _audit.LogAsync("policy.pdf_uploaded", actor, tenantId: tenantId,
            resourceType: "policy", resourceId: policy.Id, metadata: new { asset.FileName, bytes = data.Length }, ct: ct);
    }

    public async Task PublishAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var policy = await Require(tenantId, id, ct);

        var hasContent = policy.DraftContentType == PolicyContentType.Html
            ? !string.IsNullOrWhiteSpace(policy.DraftHtmlContent)
            : policy.DraftAssetId is not null;
        if (!hasContent)
            throw new PolicyInvalidException("Add content (HTML or a PDF) before publishing.");

        var version = new PolicyVersion
        {
            PolicyId = policy.Id,
            TenantId = tenantId,
            Version = await _policies.NextVersionNumberAsync(policy.Id, ct),
            ContentType = policy.DraftContentType,
            HtmlContent = policy.DraftContentType == PolicyContentType.Html ? policy.DraftHtmlContent : null,
            AssetId = policy.DraftContentType == PolicyContentType.Pdf ? policy.DraftAssetId : null,
            PublishedAt = DateTimeOffset.UtcNow
        };
        await _policies.AddVersionAsync(version, ct);

        policy.CurrentVersionId = version.Id;
        policy.Status = PolicyStatus.Published;
        policy.PublishedAt = DateTimeOffset.UtcNow;
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        await _policies.UpdateAsync(policy, ct);

        await _audit.LogAsync("policy.published", actor, tenantId: tenantId,
            resourceType: "policy", resourceId: policy.Id,
            metadata: new { version = version.Version, mode = policy.EnforcementMode.ToString() }, ct: ct);
    }

    public async Task ArchiveAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var policy = await Require(tenantId, id, ct);
        policy.Status = PolicyStatus.Archived;
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        await _policies.UpdateAsync(policy, ct);
        await _audit.LogAsync("policy.archived", actor, tenantId: tenantId,
            resourceType: "policy", resourceId: policy.Id, ct: ct);
    }

    public async Task ReRequestAcceptanceAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var policy = await Require(tenantId, id, ct);
        policy.ConsentResetAt = DateTimeOffset.UtcNow;
        policy.UpdatedAt = DateTimeOffset.UtcNow;
        await _policies.UpdateAsync(policy, ct);
        await _audit.LogAsync("policy.consent_re_requested", actor, tenantId: tenantId,
            resourceType: "policy", resourceId: policy.Id, ct: ct);
    }

    public Task<PolicyAsset?> GetAssetAsync(Guid id, CancellationToken ct = default)
        => _policies.GetAssetAsync(id, ct);

    private async Task<Policy> Require(Guid tenantId, Guid id, CancellationToken ct)
        => await _policies.GetAsync(tenantId, id, ct)
           ?? throw new KeyNotFoundException($"Policy {id} not found.");

    private static string? Normalize(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
