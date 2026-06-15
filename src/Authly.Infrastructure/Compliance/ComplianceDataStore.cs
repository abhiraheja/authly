using Authly.Core.Compliance;
using Authly.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Authly.Infrastructure.Compliance;

/// <summary>
/// Reads/erases a single user's data within their tenant via direct EF access. Every query is
/// tenant-scoped (RLS backstops it too). Export omits all credentials/secrets; erasure relies on
/// the schema's ON DELETE CASCADE to clear the user's child rows.
/// </summary>
public sealed class ComplianceDataStore : IComplianceDataStore
{
    private readonly AppDbContext _db;

    public ComplianceDataStore(AppDbContext db) => _db = db;

    public async Task<UserDataExport?> ExportUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == userId, ct);
        if (user is null) return null;

        var profile = new ExportedProfile(
            user.Id, user.Email, user.EmailVerified, user.Username, user.Phone, user.PhoneVerified,
            user.Status.ToString(), user.FirstName, user.LastName, user.Timezone, user.Locale,
            user.UserMetadata, user.CreatedAt, user.LastLoginAt);

        var roles = await (
            from ur in _db.UserRoles.AsNoTracking()
            join r in _db.Roles.AsNoTracking() on ur.RoleId equals r.Id
            where ur.TenantId == tenantId && ur.UserId == userId
            orderby r.Name
            select r.Name).ToListAsync(ct);

        var sessions = await _db.Sessions.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new ExportedSession(s.Id, s.IpAddress, s.UserAgent, s.Location, s.Trusted,
                s.CreatedAt, s.ExpiresAt, s.Revoked))
            .ToListAsync(ct);

        var logins = await _db.LoginHistory.AsNoTracking()
            .Where(h => h.TenantId == tenantId && h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new ExportedLogin(h.Result, h.Method, h.IpAddress, h.UserAgent, h.Location, h.Reason, h.CreatedAt))
            .ToListAsync(ct);

        var factors = await _db.MfaFactors.AsNoTracking()
            .Where(f => f.TenantId == tenantId && f.UserId == userId)
            .Select(f => new ExportedMfaFactor(f.Type.ToString(), f.Status.ToString(), f.FriendlyName, f.CreatedAt))
            .ToListAsync(ct);

        var socials = await _db.SocialIdentities.AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.UserId == userId)
            .Select(s => new ExportedSocialIdentity(s.Provider, s.ProviderEmail, s.CreatedAt))
            .ToListAsync(ct);

        var recovery = await _db.RecoveryContacts.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.UserId == userId)
            .Select(c => new ExportedRecoveryContact(c.Type.ToString(), c.Value, c.Verified, c.CreatedAt))
            .ToListAsync(ct);

        var consents = await _db.ConsentRecords.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.UserId == userId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new ExportedConsent(c.Purpose, c.Granted, c.Version, c.CreatedAt))
            .ToListAsync(ct);

        return new UserDataExport(DateTimeOffset.UtcNow, profile, roles, sessions, logins,
            factors, socials, recovery, consents);
    }

    public async Task<bool> EraseUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        // Tenant-scoped delete; child rows (sessions, login history, MFA, social, recovery,
        // consent, verification/reset tokens, api keys, user_roles) cascade via their user FK.
        var deleted = await _db.Users
            .Where(u => u.TenantId == tenantId && u.Id == userId)
            .ExecuteDeleteAsync(ct);
        return deleted > 0;
    }
}
