using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Observability;

/// <summary>Form input for the observability admin page. Secret fields are write-only: a null/blank
/// value means "keep the stored secret unchanged".</summary>
public sealed class ObservabilityConfigInput
{
    public bool Enabled { get; set; }
    public string Exporter { get; set; } = "otlp";
    public string? OtlpEndpoint { get; set; }
    public string? OtlpHeaders { get; set; }              // secret (write-only)
    public string? AzureConnectionString { get; set; }    // secret (write-only)
    public string Signals { get; set; } = "traces,metrics,logs";
    public double SamplingRatio { get; set; } = 1.0;
    public string? LogStreamEndpoint { get; set; }
    public string? LogStreamKey { get; set; }             // secret (write-only)
}

/// <summary>Non-secret view of the config for the edit form (secrets surfaced only as "configured?" flags).</summary>
public sealed record ObservabilityConfigView(
    bool Enabled, string Exporter, string? OtlpEndpoint, bool HasOtlpHeaders,
    bool HasAzureConnectionString, string Signals, double SamplingRatio,
    string? LogStreamEndpoint, bool HasLogStreamKey, DateTimeOffset? UpdatedAt);

/// <summary>Fully-resolved, decrypted settings for runtime use (startup OTel wiring, LogStreamJob).</summary>
public sealed record ObservabilitySettings(
    bool Enabled, string Exporter, string? OtlpEndpoint, string? OtlpHeaders,
    string? AzureConnectionString, IReadOnlySet<string> Signals, double SamplingRatio,
    string? LogStreamEndpoint, string? LogStreamKey)
{
    public static readonly ObservabilitySettings Disabled = new(
        false, "otlp", null, null, null, new HashSet<string>(), 1.0, null, null);

    public bool ExportTraces => Signals.Contains("traces");
    public bool ExportMetrics => Signals.Contains("metrics");
    public bool ExportLogs => Signals.Contains("logs");

    /// <summary>True when an exporter has a usable destination configured.</summary>
    public bool HasExporterTarget => Exporter == "azure_monitor"
        ? !string.IsNullOrWhiteSpace(AzureConnectionString)
        : !string.IsNullOrWhiteSpace(OtlpEndpoint);
}

/// <summary>
/// Reads and writes the instance-global observability config, encrypting secrets on write (BYOK
/// pattern, mirroring <c>MessagingService</c>). Runtime callers get decrypted <see cref="ObservabilitySettings"/>;
/// the admin form gets a secret-free <see cref="ObservabilityConfigView"/>.
/// </summary>
public interface IObservabilityConfigService
{
    Task<ObservabilitySettings> GetSettingsAsync(CancellationToken ct = default);
    Task<ObservabilityConfigView> GetForEditAsync(CancellationToken ct = default);
    Task SaveAsync(ObservabilityConfigInput input, AuditContext actor, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ObservabilityConfigService : IObservabilityConfigService
{
    private readonly IObservabilityConfigRepository _repo;
    private readonly IEncryptionService _encryption;
    private readonly IAuditLogger _audit;

    public ObservabilityConfigService(IObservabilityConfigRepository repo, IEncryptionService encryption, IAuditLogger audit)
    {
        _repo = repo;
        _encryption = encryption;
        _audit = audit;
    }

    public async Task<ObservabilitySettings> GetSettingsAsync(CancellationToken ct = default)
    {
        var c = await _repo.GetAsync(ct);
        if (c is null) return ObservabilitySettings.Disabled;

        return new ObservabilitySettings(
            c.Enabled, c.Exporter, c.OtlpEndpoint,
            Decrypt(c.OtlpHeadersEncrypted),
            Decrypt(c.AzureConnectionStringEncrypted),
            ParseSignals(c.Signals),
            c.SamplingRatio,
            c.LogStreamEndpoint,
            Decrypt(c.LogStreamKeyEncrypted));
    }

    public async Task<ObservabilityConfigView> GetForEditAsync(CancellationToken ct = default)
    {
        var c = await _repo.GetAsync(ct);
        if (c is null)
            return new ObservabilityConfigView(false, "otlp", null, false, false, "traces,metrics,logs", 1.0, null, false, null);

        return new ObservabilityConfigView(
            c.Enabled, c.Exporter, c.OtlpEndpoint,
            !string.IsNullOrEmpty(c.OtlpHeadersEncrypted),
            !string.IsNullOrEmpty(c.AzureConnectionStringEncrypted),
            c.Signals, c.SamplingRatio, c.LogStreamEndpoint,
            !string.IsNullOrEmpty(c.LogStreamKeyEncrypted), c.UpdatedAt);
    }

    public async Task SaveAsync(ObservabilityConfigInput input, AuditContext actor, CancellationToken ct = default)
    {
        var existing = await _repo.GetAsync(ct);

        var config = existing ?? new ObservabilityConfig();
        config.Enabled = input.Enabled;
        config.Exporter = input.Exporter == "azure_monitor" ? "azure_monitor" : "otlp";
        config.OtlpEndpoint = Trim(input.OtlpEndpoint);
        config.Signals = NormalizeSignals(input.Signals);
        config.SamplingRatio = Math.Clamp(input.SamplingRatio, 0.0, 1.0);
        config.LogStreamEndpoint = Trim(input.LogStreamEndpoint);

        // Secrets: encrypt a freshly-entered value, otherwise keep the stored ciphertext.
        config.OtlpHeadersEncrypted = EncryptOrKeep(input.OtlpHeaders, existing?.OtlpHeadersEncrypted);
        config.AzureConnectionStringEncrypted = EncryptOrKeep(input.AzureConnectionString, existing?.AzureConnectionStringEncrypted);
        config.LogStreamKeyEncrypted = EncryptOrKeep(input.LogStreamKey, existing?.LogStreamKeyEncrypted);

        await _repo.UpsertAsync(config, ct);
        await _audit.LogAsync("observability.config_saved", actor,
            resourceType: "observability_config",
            metadata: new { config.Enabled, config.Exporter, config.Signals }, ct: ct);
    }

    private string? EncryptOrKeep(string? newValue, string? existingCiphertext)
        => string.IsNullOrWhiteSpace(newValue) ? existingCiphertext : _encryption.Encrypt(newValue.Trim());

    private string? Decrypt(string? ciphertext)
        => string.IsNullOrEmpty(ciphertext) ? null : _encryption.Decrypt(ciphertext);

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static IReadOnlySet<string> ParseSignals(string signals)
        => signals.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant()).ToHashSet();

    private static string NormalizeSignals(string signals)
    {
        var allowed = new[] { "traces", "metrics", "logs" };
        var picked = ParseSignals(signals).Where(allowed.Contains);
        return string.Join(",", allowed.Where(picked.Contains));
    }
}
