namespace Authly.Core.Entities;

/// <summary>
/// A tenant-uploaded branding image (logo or login background) stored as bytes in PostgreSQL and
/// served by the public <c>/branding/asset/{id}</c> endpoint. Kept in the database (not on disk)
/// so a self-hosted instance needs no shared volume and survives container rebuilds. Maps to
/// table "branding_assets".
/// </summary>
public class BrandingAsset
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant — assets are tenant-scoped.</summary>
    public Guid TenantId { get; set; }

    /// <summary>What the asset is used for: <c>logo</c> or <c>background</c>. One current asset per kind.</summary>
    public string Kind { get; set; } = default!;

    /// <summary>MIME type to serve the bytes with (e.g. <c>image/png</c>).</summary>
    public string ContentType { get; set; } = default!;

    /// <summary>The raw image bytes.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public DateTimeOffset CreatedAt { get; set; }
}
