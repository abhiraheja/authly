namespace Authly.Core.Entities;

/// <summary>
/// Instance-global observability configuration (single row, no TenantId, NO RLS). Drives the
/// OpenTelemetry exporters wired at startup and the audit log-stream target. Secret fields are
/// stored encrypted via <c>IEncryptionService</c>; nothing here is tenant-scoped. Maps to table
/// "observability_config".
/// </summary>
public class ObservabilityConfig
{
    public Guid Id { get; set; }

    /// <summary>Master switch — when false, no exporter is wired (changes apply on restart).</summary>
    public bool Enabled { get; set; }

    /// <summary>Exporter backend: <c>otlp</c> or <c>azure_monitor</c>.</summary>
    public string Exporter { get; set; } = "otlp";

    /// <summary>OTLP collector endpoint (e.g. <c>http://otel-collector:4317</c>). Not secret.</summary>
    public string? OtlpEndpoint { get; set; }

    /// <summary>Encrypted OTLP headers (e.g. <c>Authorization=Bearer …</c>, comma-separated key=value).</summary>
    public string? OtlpHeadersEncrypted { get; set; }

    /// <summary>Encrypted Azure Monitor connection string (when <see cref="Exporter"/> is azure_monitor).</summary>
    public string? AzureConnectionStringEncrypted { get; set; }

    /// <summary>Comma-separated signals to export: any of <c>traces,metrics,logs</c>.</summary>
    public string Signals { get; set; } = "traces,metrics,logs";

    /// <summary>Head sampling ratio for traces, 0.0–1.0.</summary>
    public double SamplingRatio { get; set; } = 1.0;

    /// <summary>Optional audit log-stream sink endpoint (re-sources the LogStreamJob target). Not secret.</summary>
    public string? LogStreamEndpoint { get; set; }

    /// <summary>Encrypted log-stream API key (sent as <c>X-Log-Stream-Key</c>).</summary>
    public string? LogStreamKeyEncrypted { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}
