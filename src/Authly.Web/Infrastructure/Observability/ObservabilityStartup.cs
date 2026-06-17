using Authly.Infrastructure.Data;
using Authly.Infrastructure.Security;
using Authly.Modules.Observability;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Authly.Web.Infrastructure.Observability;

/// <summary>
/// Wires OpenTelemetry from the stored <see cref="ObservabilityConfig"/> at startup. Read once,
/// before the host is built (so it can configure logging/tracing/metrics providers); changes apply
/// on the next restart. When no config row is enabled, an <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> env var
/// is honored as a fallback; absent both, nothing is exported.
/// </summary>
public static class ObservabilityStartup
{
    private const string ServiceName = "authly";

    public static void AddAuthlyObservability(this WebApplicationBuilder builder)
    {
        var s = ResolveAtStartup(builder.Configuration);
        if (!s.Enabled || !s.HasExporterTarget) return;

        builder.Services.AddHttpContextAccessor();

        var otel = builder.Services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(ServiceName));

        if (s.ExportTraces)
            otel.WithTracing(t =>
            {
                t.SetSampler(new TraceIdRatioBasedSampler(s.SamplingRatio));
                t.AddAspNetCoreInstrumentation();
                t.AddHttpClientInstrumentation();
                t.AddProcessor<TelemetryEnrichmentProcessor>();
                ApplyTraceExporter(t, s);
            });

        if (s.ExportMetrics)
            otel.WithMetrics(m =>
            {
                m.AddAspNetCoreInstrumentation();
                m.AddHttpClientInstrumentation();
                m.AddRuntimeInstrumentation();
                ApplyMetricExporter(m, s);
            });

        if (s.ExportLogs)
            builder.Logging.AddOpenTelemetry(l =>
            {
                l.IncludeFormattedMessage = true;
                l.IncludeScopes = true;
                l.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService(ServiceName));
                ApplyLogExporter(l, s);
            });
    }

    private static void ApplyTraceExporter(TracerProviderBuilder t, ObservabilitySettings s)
    {
        if (s.Exporter == "azure_monitor")
            t.AddAzureMonitorTraceExporter(o => o.ConnectionString = s.AzureConnectionString);
        else
            t.AddOtlpExporter(o => ConfigureOtlp(o, s));
    }

    private static void ApplyMetricExporter(MeterProviderBuilder m, ObservabilitySettings s)
    {
        if (s.Exporter == "azure_monitor")
            m.AddAzureMonitorMetricExporter(o => o.ConnectionString = s.AzureConnectionString);
        else
            m.AddOtlpExporter(o => ConfigureOtlp(o, s));
    }

    private static void ApplyLogExporter(OpenTelemetryLoggerOptions l, ObservabilitySettings s)
    {
        if (s.Exporter == "azure_monitor")
            l.AddAzureMonitorLogExporter(o => o.ConnectionString = s.AzureConnectionString);
        else
            l.AddOtlpExporter(o => ConfigureOtlp(o, s));
    }

    private static void ConfigureOtlp(OpenTelemetry.Exporter.OtlpExporterOptions o, ObservabilitySettings s)
    {
        if (!string.IsNullOrWhiteSpace(s.OtlpEndpoint)) o.Endpoint = new Uri(s.OtlpEndpoint);
        if (!string.IsNullOrWhiteSpace(s.OtlpHeaders)) o.Headers = s.OtlpHeaders;
    }

    /// <summary>
    /// Reads the stored config (decrypting secrets) before the host is built. Best-effort — any
    /// failure (table missing on a fresh DB, bad key) yields disabled telemetry. An env OTLP endpoint
    /// is honored when no enabled row exists.
    /// </summary>
    private static ObservabilitySettings ResolveAtStartup(IConfiguration config)
    {
        try
        {
            var conn = config["DATABASE_URL"] ?? "Host=localhost;Database=authly;Username=authly;Password=authly";
            var options = new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(conn).Options;
            using var db = new AppDbContext(options);
            var row = db.ObservabilityConfigs.OrderBy(c => c.UpdatedAt).FirstOrDefault();

            if (row is { Enabled: true })
            {
                var enc = new AesEncryptionService(Options.Create(new EncryptionOptions { Key = config["ENCRYPTION_KEY"]! }));
                string? Dec(string? c) => string.IsNullOrEmpty(c) ? null : enc.Decrypt(c);
                var signals = row.Signals.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(x => x.ToLowerInvariant()).ToHashSet();
                return new ObservabilitySettings(true, row.Exporter, row.OtlpEndpoint, Dec(row.OtlpHeadersEncrypted),
                    Dec(row.AzureConnectionStringEncrypted), signals, row.SamplingRatio, row.LogStreamEndpoint, Dec(row.LogStreamKeyEncrypted));
            }
        }
        catch
        {
            // Telemetry is opt-in and non-critical — never block startup on a config read.
        }

        var envEndpoint = config["OTEL_EXPORTER_OTLP_ENDPOINT"];
        if (!string.IsNullOrWhiteSpace(envEndpoint))
            return new ObservabilitySettings(true, "otlp", envEndpoint, null, null,
                new HashSet<string> { "traces", "metrics", "logs" }, 1.0, null, null);

        return ObservabilitySettings.Disabled;
    }
}
