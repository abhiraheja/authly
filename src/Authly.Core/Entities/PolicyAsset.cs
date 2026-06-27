namespace Authly.Core.Entities;

/// <summary>
/// An uploaded policy document (PDF) stored as bytes in PostgreSQL — same approach as
/// <see cref="BrandingAsset"/>, so a self-hosted instance needs no shared volume and survives
/// container rebuilds. Served by the consent page's asset endpoint. Maps to table "policy_assets".
/// </summary>
public class PolicyAsset
{
    public Guid Id { get; set; }

    /// <summary>Owning tenant — assets are tenant-scoped.</summary>
    public Guid TenantId { get; set; }

    /// <summary>The policy this document belongs to.</summary>
    public Guid PolicyId { get; set; }

    /// <summary>Original file name (for display / download).</summary>
    public string FileName { get; set; } = default!;

    /// <summary>MIME type to serve the bytes with (e.g. <c>application/pdf</c>).</summary>
    public string ContentType { get; set; } = default!;

    /// <summary>The raw document bytes.</summary>
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public int SizeBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
