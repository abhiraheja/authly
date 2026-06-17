using Authly.Core.Common;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Core.WebAuthn;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Common;

namespace Authly.Tests.AdvancedAuth;

// Shared in-memory fakes for the Phase 11 advanced-auth service tests.

internal sealed class FakeUserRepo : IUserRepository
{
    public readonly List<User> Items = new();
    public Task<User?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(u => u.TenantId == tenantId && u.Id == id));
    public Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(u => u.TenantId == tenantId && u.Email == email));
    public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct = default)
        => Task.FromResult(Items.Any(u => u.TenantId == tenantId && u.Email == email));
    public Task<IReadOnlyList<User>> ListByTenantAsync(Guid tenantId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<User>>(Items.Where(u => u.TenantId == tenantId).ToList());
    public Task<PagedResult<User>> ListPagedAsync(Guid tenantId, Pagination page, string? emailContains = null, CancellationToken ct = default)
        => throw new NotImplementedException();
    public Task DeleteAsync(User user, CancellationToken ct = default) { Items.Remove(user); return Task.CompletedTask; }
    public Task<bool> AnyTenantAdminAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(false);
    public Task AddAsync(User user, CancellationToken ct = default)
    {
        if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
        Items.Add(user);
        return Task.CompletedTask;
    }
    public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeVerificationRepo : IVerificationTokenRepository
{
    public readonly List<VerificationToken> Items = new();
    public Task<VerificationToken?> GetByHashAsync(string hash, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == hash));
    public Task AddAsync(VerificationToken token, CancellationToken ct = default) { Items.Add(token); return Task.CompletedTask; }
    public Task UpdateAsync(VerificationToken token, CancellationToken ct = default) => Task.CompletedTask;
    public Task InvalidateOutstandingAsync(Guid userId, string type, CancellationToken ct = default)
    {
        foreach (var t in Items.Where(t => t.UserId == userId && t.Type == type && !t.Used)) t.Used = true;
        return Task.CompletedTask;
    }
}

internal sealed class FakeResetRepo : IPasswordResetTokenRepository
{
    public readonly List<PasswordResetToken> Items = new();
    public Task<PasswordResetToken?> GetByHashAsync(string hash, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == hash));
    public Task AddAsync(PasswordResetToken token, CancellationToken ct = default) { Items.Add(token); return Task.CompletedTask; }
    public Task UpdateAsync(PasswordResetToken token, CancellationToken ct = default) => Task.CompletedTask;
    public Task InvalidateOutstandingAsync(Guid userId, CancellationToken ct = default)
    {
        foreach (var t in Items.Where(t => t.UserId == userId && !t.Used)) t.Used = true;
        return Task.CompletedTask;
    }
}

internal sealed class FakePendingChangeRepo : IPendingContactChangeRepository
{
    public readonly List<PendingContactChange> Items = new();
    public Task<PendingContactChange?> GetPendingByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.UserId == userId && c.Status == ContactChangeStatus.Pending));
    public Task<PendingContactChange?> GetByVerifyHashAsync(string hash, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(c => c.VerifyTokenHash == hash));
    public Task<PendingContactChange?> GetByCancelHashAsync(string hash, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(c => c.CancelTokenHash == hash));
    public Task AddAsync(PendingContactChange change, CancellationToken ct = default) { Items.Add(change); return Task.CompletedTask; }
    public Task UpdateAsync(PendingContactChange change, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class FakeRecoveryContactRepo : IRecoveryContactRepository
{
    public readonly List<RecoveryContact> Items = new();
    public Task<RecoveryContact?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(c => c.TenantId == tenantId && c.Id == id));
    public Task<IReadOnlyList<RecoveryContact>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<RecoveryContact>>(Items.Where(c => c.TenantId == tenantId && c.UserId == userId).ToList());
    public Task AddAsync(RecoveryContact contact, CancellationToken ct = default)
    {
        if (contact.Id == Guid.Empty) contact.Id = Guid.NewGuid();
        Items.Add(contact);
        return Task.CompletedTask;
    }
    public Task UpdateAsync(RecoveryContact contact, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(RecoveryContact contact, CancellationToken ct = default) { Items.Remove(contact); return Task.CompletedTask; }
}

internal sealed class FakeMfaFactorRepo : IMfaFactorRepository
{
    public readonly List<MfaFactor> Items = new();
    public Task<MfaFactor?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(f => f.TenantId == tenantId && f.Id == id));
    public Task<IReadOnlyList<MfaFactor>> ListByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MfaFactor>>(Items.Where(f => f.TenantId == tenantId && f.UserId == userId).ToList());
    public Task<IReadOnlyList<MfaFactor>> ListActiveByUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MfaFactor>>(Items.Where(f => f.TenantId == tenantId && f.UserId == userId && f.Status == MfaFactorStatus.Active).ToList());
    public Task<MfaFactor?> GetActiveByTypeAsync(Guid tenantId, Guid userId, MfaFactorType type, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(f => f.TenantId == tenantId && f.UserId == userId && f.Type == type && f.Status == MfaFactorStatus.Active));
    public Task<IReadOnlyList<MfaFactor>> ListActiveByTypeAsync(Guid tenantId, Guid userId, MfaFactorType type, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<MfaFactor>>(Items.Where(f => f.TenantId == tenantId && f.UserId == userId && f.Type == type && f.Status == MfaFactorStatus.Active).ToList());
    public Task<bool> AnyActiveAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => Task.FromResult(Items.Any(f => f.TenantId == tenantId && f.UserId == userId && f.Status == MfaFactorStatus.Active));
    public Task AddAsync(MfaFactor factor, CancellationToken ct = default)
    {
        if (factor.Id == Guid.Empty) factor.Id = Guid.NewGuid();
        Items.Add(factor);
        return Task.CompletedTask;
    }
    public Task UpdateAsync(MfaFactor factor, CancellationToken ct = default) => Task.CompletedTask;
}

internal sealed class CapturingQueue : IMessageQueue
{
    public readonly List<MessageSendRequest> Items = new();
    public void Enqueue(MessageSendRequest request) => Items.Add(request);
}

internal sealed class CapturingUrlBuilder : IAuthUrlBuilder
{
    public string? LastMagic, LastVerify, LastCancel, LastRecovery;
    public string BuildEmailVerificationUrl(Guid t, string raw) => $"https://test/verify?token={raw}";
    public string BuildPasswordResetUrl(Guid t, string raw) => $"https://test/reset?token={raw}";
    public string BuildMagicLinkUrl(Guid t, string raw) { LastMagic = raw; return $"https://test/magic?token={raw}"; }
    public string BuildContactChangeVerifyUrl(Guid t, string raw) { LastVerify = raw; return $"https://test/change/verify?token={raw}"; }
    public string BuildContactChangeCancelUrl(Guid t, string raw) { LastCancel = raw; return $"https://test/change/cancel?token={raw}"; }
    public string BuildRecoveryUrl(Guid t, string raw) { LastRecovery = raw; return $"https://test/recover?token={raw}"; }
    public string BuildInviteAcceptUrl(string raw) => $"https://test/invite/accept?token={raw}";
}

internal sealed class RecordingAudit : IAuditLogger
{
    public readonly List<string> Events = new();
    public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
        Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
    { Events.Add(@event); return Task.CompletedTask; }
}

internal sealed class FakeWebAuthnGateway : IWebAuthnGateway
{
    public IReadOnlyList<byte[]> LastExclude = Array.Empty<byte[]>();
    public IReadOnlyList<byte[]> LastAllowed = Array.Empty<byte[]>();
    public byte[] NewCredentialId = { 9, 9, 9 };

    public WebAuthnChallenge BeginRegistration(WebAuthnUser user, IReadOnlyList<byte[]> excludeCredentialIds)
    {
        LastExclude = excludeCredentialIds;
        return new WebAuthnChallenge("{\"reg\":true}", "state-reg");
    }
    public Task<WebAuthnNewCredential> CompleteRegistrationAsync(string state, string responseJson, CancellationToken ct = default)
        => Task.FromResult(new WebAuthnNewCredential(NewCredentialId, new byte[] { 1, 2, 3, 4 }, 0, Guid.Empty));

    public WebAuthnChallenge BeginAssertion(IReadOnlyList<byte[]> allowedCredentialIds)
    {
        LastAllowed = allowedCredentialIds;
        return new WebAuthnChallenge("{\"assert\":true}", "state-assert");
    }
    // Echoes the first stored credential back with an incremented counter (simulates a valid assertion).
    public Task<WebAuthnAssertionResult> CompleteAssertionAsync(string state, string responseJson,
        IReadOnlyList<WebAuthnStoredCredential> credentials, CancellationToken ct = default)
    {
        var c = credentials[0];
        return Task.FromResult(new WebAuthnAssertionResult(c.CredentialId, c.SignCount + 1));
    }
}
