using Authly.Core.OAuth;

namespace Authly.Core.Interfaces;

/// <summary>
/// Abstraction over the OAuth server's client registry (OpenIddict). Lets the business layer
/// create/update/delete protocol clients without depending on OpenIddict directly. Implemented
/// in the composition root over <c>IOpenIddictApplicationManager</c>.
/// </summary>
public interface IOAuthClientStore
{
    Task CreateClientAsync(OAuthClientDescriptor descriptor, CancellationToken ct = default);
    Task UpdateClientAsync(OAuthClientDescriptor descriptor, CancellationToken ct = default);
    Task DeleteClientAsync(string clientId, CancellationToken ct = default);

    /// <summary>Replaces the confidential client's secret (rotation).</summary>
    Task SetClientSecretAsync(string clientId, string rawSecret, CancellationToken ct = default);
}
