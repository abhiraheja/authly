using System.Buffers.Text;
using System.Text.Json;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.WebAuthn;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.AdvancedAuth;

/// <inheritdoc />
public sealed class PasskeyService : IPasskeyService
{
    private readonly IMfaFactorRepository _factors;
    private readonly IUserRepository _users;
    private readonly IWebAuthnGateway _webAuthn;
    private readonly IAuditLogger _audit;

    public PasskeyService(IMfaFactorRepository factors, IUserRepository users, IWebAuthnGateway webAuthn, IAuditLogger audit)
    {
        _factors = factors;
        _users = users;
        _webAuthn = webAuthn;
        _audit = audit;
    }

    public async Task<WebAuthnChallenge> BeginRegistrationAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(tenantId, userId, ct)
            ?? throw new AdvancedAuthException("User not found.");

        var existing = await _factors.ListActiveByTypeAsync(tenantId, userId, MfaFactorType.Passkey, ct);
        var exclude = existing.Where(f => f.CredentialId is not null)
            .Select(f => Base64Url.DecodeFromChars(f.CredentialId)).ToList();

        var handle = user.Id.ToByteArray();
        var name = user.Email;
        var display = string.IsNullOrWhiteSpace(user.FirstName) ? user.Email : user.FirstName!;
        return _webAuthn.BeginRegistration(new WebAuthnUser(handle, name, display), exclude);
    }

    public async Task CompleteRegistrationAsync(Guid tenantId, Guid userId, string state, string responseJson,
        string? friendlyName, AuditContext actor, CancellationToken ct = default)
    {
        var credential = await _webAuthn.CompleteRegistrationAsync(state, responseJson, ct);

        await _factors.AddAsync(new MfaFactor
        {
            TenantId = tenantId,
            UserId = userId,
            Type = MfaFactorType.Passkey,
            Status = MfaFactorStatus.Active,
            CredentialId = Base64Url.EncodeToString(credential.CredentialId),
            Secret = SerializeCredential(credential.PublicKey, credential.SignCount, credential.Aaguid),
            FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? "Passkey" : friendlyName!.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        await _audit.LogAsync("user.passkey_registered", actor, tenantId, "user", userId, ct: ct);
    }

    public async Task<WebAuthnChallenge?> BeginLoginAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var passkeys = await _factors.ListActiveByTypeAsync(tenantId, userId, MfaFactorType.Passkey, ct);
        var allowed = passkeys.Where(f => f.CredentialId is not null)
            .Select(f => Base64Url.DecodeFromChars(f.CredentialId)).ToList();
        if (allowed.Count == 0) return null;

        return _webAuthn.BeginAssertion(allowed);
    }

    public async Task<User?> CompleteLoginAsync(Guid tenantId, Guid userId, string state, string responseJson, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(tenantId, userId, ct);
        if (user is null || user.Status != UserStatus.Active) return null;

        var passkeys = await _factors.ListActiveByTypeAsync(tenantId, userId, MfaFactorType.Passkey, ct);
        var stored = new List<WebAuthnStoredCredential>();
        foreach (var f in passkeys)
        {
            if (f.CredentialId is null || f.Secret is null) continue;
            var (pk, sc, _) = DeserializeCredential(f.Secret);
            stored.Add(new WebAuthnStoredCredential(Base64Url.DecodeFromChars(f.CredentialId), pk, sc, user.Id.ToByteArray()));
        }
        if (stored.Count == 0) return null;

        WebAuthnAssertionResult result;
        try
        {
            result = await _webAuthn.CompleteAssertionAsync(state, responseJson, stored, ct);
        }
        catch (WebAuthnException)
        {
            return null;
        }

        // Persist the new signature counter (clone-detection) and mark the credential used.
        var matchedId = Base64Url.EncodeToString(result.CredentialId);
        var factor = passkeys.FirstOrDefault(f => f.CredentialId == matchedId);
        if (factor is not null)
        {
            var (pk, _, aaguid) = DeserializeCredential(factor.Secret!);
            factor.Secret = SerializeCredential(pk, result.NewSignCount, aaguid);
            factor.LastUsedAt = DateTimeOffset.UtcNow;
            await _factors.UpdateAsync(factor, ct);
        }

        return user;
    }

    public async Task<IReadOnlyList<PasskeySummary>> ListAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => (await _factors.ListActiveByTypeAsync(tenantId, userId, MfaFactorType.Passkey, ct))
            .Select(f => new PasskeySummary(f.Id, f.FriendlyName, f.CreatedAt, f.LastUsedAt))
            .ToList();

    public async Task RemoveAsync(Guid tenantId, Guid userId, Guid factorId, AuditContext actor, CancellationToken ct = default)
    {
        var factor = await _factors.GetByIdAsync(tenantId, factorId, ct);
        if (factor is null || factor.UserId != userId || factor.Type != MfaFactorType.Passkey) return; // ownership guard

        factor.Status = MfaFactorStatus.Revoked;
        await _factors.UpdateAsync(factor, ct);
        await _audit.LogAsync("user.passkey_removed", actor, tenantId, "user", userId, ct: ct);
    }

    // --- credential (de)serialization (public key + counter + aaguid; not secret) ---

    private static string SerializeCredential(byte[] publicKey, uint signCount, Guid aaguid)
        => JsonSerializer.Serialize(new StoredPasskey(Convert.ToBase64String(publicKey), signCount, aaguid));

    private static (byte[] PublicKey, uint SignCount, Guid Aaguid) DeserializeCredential(string json)
    {
        var data = JsonSerializer.Deserialize<StoredPasskey>(json)
                   ?? throw new AdvancedAuthException("Corrupt passkey credential.");
        return (Convert.FromBase64String(data.Pk), data.Sc, data.Aaguid);
    }

    private sealed record StoredPasskey(string Pk, uint Sc, Guid Aaguid);
}
