using System.Text.Json;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Announcements;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Tenants;

namespace Authly.Tests.Phase14;

public class OnboardingAndAnnouncementTests
{
    // --- TenantService onboarding flag ------------------------------------

    [Fact]
    public async Task Tenant_is_not_onboarded_by_default()
    {
        var repo = new FakeTenantRepo();
        var t = await Seed(repo, "{}");
        var svc = new TenantService(repo, new RecordingAudit());

        Assert.False(await svc.IsOnboardedAsync(t.Id));
    }

    [Fact]
    public async Task SetOnboarded_marks_complete_preserves_other_settings_and_audits_once()
    {
        var repo = new FakeTenantRepo();
        var t = await Seed(repo, "{\"theme\":\"dark\",\"limit\":5}");
        var audit = new RecordingAudit();
        var svc = new TenantService(repo, audit);

        await svc.SetOnboardedAsync(t.Id, AuditContext.System);

        Assert.True(await svc.IsOnboardedAsync(t.Id));
        // Existing keys survive the write.
        using var doc = JsonDocument.Parse(t.Settings);
        Assert.Equal("dark", doc.RootElement.GetProperty("theme").GetString());
        Assert.Equal(5, doc.RootElement.GetProperty("limit").GetInt32());
        Assert.Equal(new[] { "tenant.onboarded" }, audit.Events);

        // Idempotent — a second call neither rewrites nor double-audits.
        await svc.SetOnboardedAsync(t.Id, AuditContext.System);
        Assert.Single(audit.Events);
    }

    [Fact]
    public async Task IsOnboarded_tolerates_malformed_settings()
    {
        var repo = new FakeTenantRepo();
        var t = await Seed(repo, "not json");
        var svc = new TenantService(repo, new RecordingAudit());

        Assert.False(await svc.IsOnboardedAsync(t.Id));
    }

    private static async Task<Tenant> Seed(FakeTenantRepo repo, string settings)
    {
        var t = new Tenant { Id = Guid.NewGuid(), Name = "Acme", Slug = "acme", Settings = settings };
        await repo.AddAsync(t);
        return t;
    }

    // --- AnnouncementService ----------------------------------------------

    [Fact]
    public async Task Create_normalizes_unknown_severity_to_info_and_audits()
    {
        var repo = new FakeAnnouncementRepo();
        var audit = new RecordingAudit();
        var svc = new AnnouncementService(repo, audit);

        var a = await svc.CreateAsync(new AnnouncementInput("  Heads up ", " body ", "explode", true, null), AuditContext.System);

        Assert.Equal("Heads up", a.Title);
        Assert.Equal("body", a.Body);
        Assert.Equal("info", a.Severity);          // unknown severity falls back
        Assert.Contains("announcement.created", audit.Events);
    }

    [Fact]
    public async Task Create_keeps_a_valid_severity()
    {
        var svc = new AnnouncementService(new FakeAnnouncementRepo(), new RecordingAudit());
        var a = await svc.CreateAsync(new AnnouncementInput("t", "b", "WARNING", true, null), AuditContext.System);
        Assert.Equal("warning", a.Severity);
    }

    [Fact]
    public async Task Visible_filters_inactive_and_expired()
    {
        var repo = new FakeAnnouncementRepo();
        var now = DateTimeOffset.UtcNow;
        repo.Items.Add(new Announcement { Id = Guid.NewGuid(), Title = "live", Body = "b", IsActive = true, ExpiresAt = now.AddDays(1) });
        repo.Items.Add(new Announcement { Id = Guid.NewGuid(), Title = "off", Body = "b", IsActive = false, ExpiresAt = null });
        repo.Items.Add(new Announcement { Id = Guid.NewGuid(), Title = "old", Body = "b", IsActive = true, ExpiresAt = now.AddDays(-1) });
        var svc = new AnnouncementService(repo, new RecordingAudit());

        var visible = await svc.ListVisibleAsync();

        Assert.Single(visible);
        Assert.Equal("live", visible[0].Title);
    }
}

// --- fakes ----------------------------------------------------------------

internal sealed class FakeTenantRepo : ITenantRepository
{
    public readonly List<Tenant> Items = new();
    public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(t => t.Id == id));
    public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(t => t.Slug == slug));
    public Task<Tenant?> GetByCustomDomainOrNullAsync(string host, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
    public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>(Items.ToList());
    public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => Task.FromResult(Items.Any(t => t.Slug == slug));
    public Task AddAsync(Tenant tenant, CancellationToken ct = default) { Items.Add(tenant); return Task.CompletedTask; }
    public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) => Task.CompletedTask; // entity mutated in place
}

internal sealed class FakeAnnouncementRepo : IAnnouncementRepository
{
    public readonly List<Announcement> Items = new();
    public Task<IReadOnlyList<Announcement>> ListAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Announcement>>(Items.OrderByDescending(a => a.CreatedAt).ToList());
    public Task<IReadOnlyList<Announcement>> ListVisibleAsync(DateTimeOffset now, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Announcement>>(Items.Where(a => a.IsVisible(now)).ToList());
    public Task<Announcement?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));
    public Task AddAsync(Announcement a, CancellationToken ct = default) { if (a.Id == Guid.Empty) a.Id = Guid.NewGuid(); Items.Add(a); return Task.CompletedTask; }
    public Task UpdateAsync(Announcement a, CancellationToken ct = default) => Task.CompletedTask;
    public Task DeleteAsync(Announcement a, CancellationToken ct = default) { Items.Remove(a); return Task.CompletedTask; }
}

internal sealed class RecordingAudit : IAuditLogger
{
    public readonly List<string> Events = new();
    public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
        Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
    { Events.Add(@event); return Task.CompletedTask; }
}
