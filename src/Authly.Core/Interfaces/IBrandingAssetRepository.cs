using Authly.Core.Entities;

namespace Authly.Core.Interfaces;

/// <summary>Persistence for <see cref="BrandingAsset"/> (uploaded logo / background bytes). Implemented in Infrastructure.</summary>
public interface IBrandingAssetRepository
{
    /// <summary>Fetches an asset by id (used by the public serve endpoint). Null when not found.</summary>
    Task<BrandingAsset?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Adds a new asset.</summary>
    Task AddAsync(BrandingAsset asset, CancellationToken ct = default);

    /// <summary>Deletes every asset of the given kind for a tenant (so a new upload replaces the old one).</summary>
    Task DeleteByKindAsync(Guid tenantId, string kind, CancellationToken ct = default);
}
