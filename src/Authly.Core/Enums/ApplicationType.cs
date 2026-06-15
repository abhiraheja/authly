namespace Authly.Core.Enums;

/// <summary>
/// Kind of OAuth client. Drives whether a client secret is required (confidential) and
/// which flows are appropriate. Persisted as text.
/// </summary>
public enum ApplicationType
{
    /// <summary>Confidential server-side web app (has a secret).</summary>
    Web,

    /// <summary>Single-page app — public client, PKCE required, no secret.</summary>
    Spa,

    /// <summary>Native/mobile app — public client, PKCE required, no secret.</summary>
    Native,

    /// <summary>Machine-to-machine service — confidential, client_credentials only.</summary>
    Machine
}
