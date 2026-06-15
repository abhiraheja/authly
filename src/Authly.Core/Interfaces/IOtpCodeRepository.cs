using Authly.Core.Entities;
using Authly.Core.Enums;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for delivered one-time passcodes. Tenant-scoped. Implemented in Infrastructure.</summary>
public interface IOtpCodeRepository
{
    Task AddAsync(OtpCode code, CancellationToken ct = default);

    /// <summary>The most recent un-used, un-expired code for a user on a channel, if any.</summary>
    Task<OtpCode?> GetLatestActiveAsync(Guid tenantId, Guid userId, OtpChannel channel, CancellationToken ct = default);

    /// <summary>Marks every outstanding code for the user on the channel as used (re-issue / cleanup).</summary>
    Task InvalidateOutstandingAsync(Guid tenantId, Guid userId, OtpChannel channel, CancellationToken ct = default);

    Task UpdateAsync(OtpCode code, CancellationToken ct = default);
}
