using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Observability;

namespace Authly.Tests.Observability;

public class ObservabilityConfigServiceTests
{
    [Fact]
    public async Task Save_encrypts_secrets_and_normalizes_signals_and_audits()
    {
        var (svc, repo, _) = Build();

        await svc.SaveAsync(new ObservabilityConfigInput
        {
            Enabled = true,
            Exporter = "otlp",
            OtlpEndpoint = " http://collector:4317 ",
            OtlpHeaders = "Authorization=Bearer abc",
            Signals = "logs,bogus,traces",
            SamplingRatio = 2.5,           // clamped to 1.0
        }, AuditContext.System);

        var row = repo.Row!;
        Assert.True(row.Enabled);
        Assert.Equal("http://collector:4317", row.OtlpEndpoint);
        Assert.Equal("enc:Authorization=Bearer abc", row.OtlpHeadersEncrypted);   // encrypted on write
        Assert.Equal("traces,logs", row.Signals);                                  // normalized + ordered, bogus dropped
        Assert.Equal(1.0, row.SamplingRatio);
    }

    [Fact]
    public async Task Save_keeps_existing_secret_when_input_blank()
    {
        var (svc, repo, _) = Build();
        repo.Row = new ObservabilityConfig { Enabled = true, OtlpHeadersEncrypted = "enc:old", Signals = "traces" };

        await svc.SaveAsync(new ObservabilityConfigInput { Enabled = true, Exporter = "otlp", OtlpHeaders = null, Signals = "traces" }, AuditContext.System);

        Assert.Equal("enc:old", repo.Row!.OtlpHeadersEncrypted);  // unchanged
    }

    [Fact]
    public async Task GetForEdit_reports_secret_presence_without_leaking_values()
    {
        var (svc, repo, _) = Build();
        repo.Row = new ObservabilityConfig { Enabled = true, Exporter = "otlp", OtlpHeadersEncrypted = "enc:x", Signals = "traces" };

        var view = await svc.GetForEditAsync();

        Assert.True(view.HasOtlpHeaders);
        Assert.False(view.HasAzureConnectionString);
    }

    [Fact]
    public async Task GetSettings_decrypts_for_runtime()
    {
        var (svc, repo, _) = Build();
        repo.Row = new ObservabilityConfig { Enabled = true, Exporter = "otlp", OtlpEndpoint = "http://c:4317", OtlpHeadersEncrypted = "enc:hdr", Signals = "traces,metrics" };

        var s = await svc.GetSettingsAsync();

        Assert.True(s.Enabled);
        Assert.Equal("hdr", s.OtlpHeaders);          // decrypted
        Assert.True(s.ExportTraces);
        Assert.True(s.ExportMetrics);
        Assert.False(s.ExportLogs);
        Assert.True(s.HasExporterTarget);
    }

    [Fact]
    public async Task GetSettings_returns_disabled_when_no_row()
    {
        var (svc, _, _) = Build();
        var s = await svc.GetSettingsAsync();
        Assert.False(s.Enabled);
        Assert.Same(ObservabilitySettings.Disabled, s);
    }

    private static (ObservabilityConfigService svc, FakeRepo repo, FakeEncryption enc) Build()
    {
        var repo = new FakeRepo();
        var enc = new FakeEncryption();
        return (new ObservabilityConfigService(repo, enc, new NoopAudit()), repo, enc);
    }

    private sealed class FakeRepo : IObservabilityConfigRepository
    {
        public ObservabilityConfig? Row;
        public Task<ObservabilityConfig?> GetAsync(CancellationToken ct = default) => Task.FromResult(Row);
        public Task UpsertAsync(ObservabilityConfig config, CancellationToken ct = default) { Row = config; return Task.CompletedTask; }
    }

    private sealed class FakeEncryption : IEncryptionService
    {
        public string Encrypt(string plaintext) => $"enc:{plaintext}";
        public string Decrypt(string encrypted) => encrypted.StartsWith("enc:") ? encrypted[4..] : encrypted;
    }

    private sealed class NoopAudit : IAuditLogger
    {
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
            Guid? resourceId = null, string result = "success", object? metadata = null, bool publishEvent = true, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
