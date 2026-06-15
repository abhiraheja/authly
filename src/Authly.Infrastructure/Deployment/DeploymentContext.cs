using System.Reflection;
using Authly.Core.Deployment;
using Microsoft.Extensions.Configuration;

namespace Authly.Infrastructure.Deployment;

/// <summary>
/// Resolves <see cref="IDeploymentContext"/> from configuration (§10 env vars). Singleton —
/// deployment shape doesn't change at runtime.
/// </summary>
public sealed class DeploymentContext : IDeploymentContext
{
    public DeploymentMode Mode { get; }
    public string Version { get; }
    public string? SyncEndpoint { get; }
    public string? SyncKey { get; }

    public DeploymentContext(IConfiguration config)
    {
        Mode = string.Equals(config["DEPLOYMENT_MODE"], "self_hosted", StringComparison.OrdinalIgnoreCase)
            ? DeploymentMode.SelfHosted
            : DeploymentMode.Cloud;

        Version = config["APP_VERSION"]
            ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString()
            ?? "0.0.0";

        SyncEndpoint = Blank(config["SYNC_ENDPOINT"]);
        SyncKey = Blank(config["SYNC_KEY"]);
    }

    public bool SyncEnabled =>
        Mode == DeploymentMode.SelfHosted && SyncEndpoint is not null && SyncKey is not null;

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
