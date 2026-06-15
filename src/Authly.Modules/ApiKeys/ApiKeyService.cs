using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.ApiKeys;

/// <inheritdoc />
public sealed class ApiKeyService : IApiKeyService
{
    private readonly IApiKeyRepository _repo;
    private readonly ICredentialGenerator _credentials;
    private readonly ITokenHasher _hasher;
    private readonly IAuditLogger _audit;

    public ApiKeyService(IApiKeyRepository repo, ICredentialGenerator credentials, ITokenHasher hasher, IAuditLogger audit)
    {
        _repo = repo;
        _credentials = credentials;
        _hasher = hasher;
        _audit = audit;
    }

    public async Task<ApiKeyResult> CreateAsync(Guid tenantId, CreateApiKeyRequest request, AuditContext actor, CancellationToken ct = default)
    {
        var rawKey = _credentials.GenerateApiKey();

        // Empty scope list means full tenant access; otherwise the supplied patterns verbatim.
        var scopes = request.Scopes.Count == 0
            ? new List<string> { "*" }
            : request.Scopes.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var key = new ApiKey
        {
            TenantId = tenantId,
            UserId = request.UserId,
            KeyHash = _hasher.Hash(rawKey),     // stored hashed; raw never persisted
            Name = request.Name.Trim(),
            Scopes = scopes,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _repo.AddAsync(key, ct);

        await _audit.LogAsync("api_key.created", actor, tenantId, "api_key", key.Id,
            metadata: new { key.Name, scopes = key.Scopes }, ct: ct);
        return new ApiKeyResult(key, rawKey);
    }

    public Task<IReadOnlyList<ApiKey>> ListAsync(Guid tenantId, CancellationToken ct = default)
        => _repo.ListByTenantAsync(tenantId, ct);

    public async Task RevokeAsync(Guid tenantId, Guid id, AuditContext actor, CancellationToken ct = default)
    {
        var key = await _repo.GetByIdAsync(tenantId, id, ct) ?? throw new ApiKeyNotFoundException(id);
        if (key.Revoked) return;

        key.Revoked = true;
        await _repo.UpdateAsync(key, ct);
        await _audit.LogAsync("api_key.revoked", actor, tenantId, "api_key", key.Id, ct: ct);
    }
}
