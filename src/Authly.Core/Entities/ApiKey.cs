namespace Authly.Core.Entities;

/// <summary>
/// A long-lived API credential for programmatic access to the Management API. The raw key is
/// shown once on creation and stored only as a SHA-256 hash. A null <see cref="UserId"/> denotes
/// a tenant-level key. <see cref="Scopes"/> holds the permission patterns the key grants
/// (<c>resource.action</c> or wildcards). Maps to table "api_keys".
/// </summary>
public class ApiKey
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>Owning user, or null for a tenant-level key.</summary>
    public Guid? UserId { get; set; }

    /// <summary>SHA-256 hash of the raw key (the raw value is never stored).</summary>
    public string KeyHash { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Permission patterns this key grants (e.g. <c>user.read</c>, <c>*</c>).</summary>
    public List<string> Scopes { get; set; } = new();

    public DateTimeOffset? ExpiresAt { get; set; }

    public bool Revoked { get; set; }

    public DateTimeOffset? LastUsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Active = not revoked and not past its (optional) expiry.</summary>
    public bool IsActive(DateTimeOffset now) => !Revoked && (ExpiresAt is null || ExpiresAt > now);
}
