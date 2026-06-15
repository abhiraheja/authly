using Authly.Core.Entities;

namespace Authly.Modules.ApiKeys;

/// <summary>
/// Request to mint an API key. <paramref name="Scopes"/> are permission patterns the key grants
/// (<c>resource.action</c> or wildcards); empty defaults to full tenant access (<c>*</c>).
/// </summary>
public sealed record CreateApiKeyRequest(string Name, IReadOnlyList<string> Scopes, Guid? UserId = null, DateTimeOffset? ExpiresAt = null);

/// <summary>Result of creating a key: the stored record plus the raw key, shown exactly once.</summary>
public sealed record ApiKeyResult(ApiKey Key, string RawKey);

public sealed class ApiKeyNotFoundException(Guid id) : Exception($"API key {id} was not found.");
