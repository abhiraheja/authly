using System.ComponentModel.DataAnnotations;

namespace Authly.Web.Areas.TenantAdmin.Models;

/// <summary>Edit form for the instance-global observability config. Secret fields are write-only:
/// leave blank to keep the stored value; the matching <c>Has*</c> flag reports whether one is set.</summary>
public sealed class ObservabilityViewModel
{
    public bool Enabled { get; set; }

    [Display(Name = "Exporter")]
    public string Exporter { get; set; } = "otlp";

    [Display(Name = "OTLP endpoint")]
    public string? OtlpEndpoint { get; set; }

    [Display(Name = "OTLP headers")]
    public string? OtlpHeaders { get; set; }
    public bool HasOtlpHeaders { get; set; }

    [Display(Name = "Azure Monitor connection string")]
    public string? AzureConnectionString { get; set; }
    public bool HasAzureConnectionString { get; set; }

    public bool ExportTraces { get; set; } = true;
    public bool ExportMetrics { get; set; } = true;
    public bool ExportLogs { get; set; } = true;

    [Display(Name = "Trace sampling ratio")]
    [Range(0.0, 1.0)]
    public double SamplingRatio { get; set; } = 1.0;

    [Display(Name = "Audit log-stream endpoint")]
    public string? LogStreamEndpoint { get; set; }

    [Display(Name = "Audit log-stream key")]
    public string? LogStreamKey { get; set; }
    public bool HasLogStreamKey { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
