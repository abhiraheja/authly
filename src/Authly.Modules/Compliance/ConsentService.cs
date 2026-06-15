using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Compliance;

public sealed class ConsentService : IConsentService
{
    private readonly IConsentRecordRepository _consents;
    private readonly IAuditLogger _audit;

    public ConsentService(IConsentRecordRepository consents, IAuditLogger audit)
    {
        _consents = consents;
        _audit = audit;
    }

    public async Task RecordAsync(Guid tenantId, Guid userId, string purpose, bool granted, string? version,
        AuditContext actor, CancellationToken ct = default)
    {
        await _consents.AddAsync(new ConsentRecord
        {
            TenantId = tenantId,
            UserId = userId,
            Purpose = purpose,
            Granted = granted,
            Version = version,
            IpAddress = actor.IpAddress,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        await _audit.LogAsync("user.consent_recorded", actor, tenantId: tenantId,
            resourceType: "user", resourceId: userId,
            metadata: new { purpose, granted, version }, ct: ct);
    }

    public async Task RecordSignupConsentAsync(Guid tenantId, Guid userId, string? policyVersion,
        AuditContext actor, CancellationToken ct = default)
    {
        await RecordAsync(tenantId, userId, ConsentPurposes.TermsOfService, true, policyVersion, actor, ct);
        await RecordAsync(tenantId, userId, ConsentPurposes.PrivacyPolicy, true, policyVersion, actor, ct);
    }

    public Task<IReadOnlyList<ConsentRecord>> ListAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _consents.ListByUserAsync(tenantId, userId, ct);
}
