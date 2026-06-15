namespace Authly.Core.Deployment;

/// <summary>How this instance is deployed. Drives the self-host telemetry sync (§9).</summary>
public enum DeploymentMode
{
    /// <summary>The managed multi-tenant SaaS we operate.</summary>
    Cloud,

    /// <summary>A customer-operated copy of the same codebase that syncs aggregate telemetry to cloud.</summary>
    SelfHosted
}

/// <summary>
/// Read-only view of the deployment configuration (env vars in §10). Resolved once from
/// configuration; carries no secrets to the modules beyond what the sync job needs. The
/// raw <see cref="SyncKey"/> is only ever read by the self-host sync job to authenticate
/// its outbound push — it is never persisted on the instance.
/// </summary>
public interface IDeploymentContext
{
    DeploymentMode Mode { get; }

    /// <summary>The running application version (reported in telemetry).</summary>
    string Version { get; }

    /// <summary>Cloud sync ingest URL — set only when <see cref="Mode"/> is SelfHosted.</summary>
    string? SyncEndpoint { get; }

    /// <summary>The per-instance sync key issued at registration — set only when SelfHosted.</summary>
    string? SyncKey { get; }

    /// <summary>True when self-hosted and both the endpoint and key are configured.</summary>
    bool SyncEnabled { get; }
}
