using Authly.Core.Branding;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Branding;
using Authly.Modules.Common;

namespace Authly.Tests.Branding;

public class BrandingServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Save_persists_validated_branding_and_audits()
    {
        var h = new Harness();
        await h.Service.SaveAsync(Tenant, new BrandingInput
        {
            LogoUrl = "https://cdn.acme.com/logo.svg",
            PrimaryColor = "#AABBCC",
            ButtonTextColor = "#FFFFFF",
            FontFamily = "Roboto, sans-serif",
            Layout = Core.Enums.BrandingLayout.FormRight,
            DarkMode = true,
            Tagline = "  Hello  "
        }, AuditContext.System);

        var saved = TenantBrandingJson.Parse(h.Tenant.Branding);
        Assert.Equal("#aabbcc", saved.PrimaryColor);                 // normalized to lower-case
        Assert.Equal(Core.Enums.BrandingLayout.FormRight, saved.Layout);
        Assert.True(saved.DarkMode);
        Assert.Equal("Hello", saved.Tagline);                        // trimmed
        Assert.Contains("tenant.branding_updated", h.Audit.Events);
    }

    [Fact]
    public async Task Save_rejects_a_non_hex_color()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<BrandingConfigInvalidException>(() =>
            h.Service.SaveAsync(Tenant, new BrandingInput { PrimaryColor = "blue" }, AuditContext.System));
    }

    [Fact]
    public async Task Save_rejects_a_non_http_logo_url()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<BrandingConfigInvalidException>(() =>
            h.Service.SaveAsync(Tenant, new BrandingInput
            {
                PrimaryColor = "#5b6df5", ButtonTextColor = "#fff", LogoUrl = "javascript:alert(1)"
            }, AuditContext.System));
    }

    [Fact]
    public async Task Save_strips_dangerous_characters_from_the_font()
    {
        var h = new Harness();
        await h.Service.SaveAsync(Tenant, new BrandingInput
        {
            PrimaryColor = "#5b6df5", ButtonTextColor = "#fff", FontFamily = "Inter</style><script>"
        }, AuditContext.System);

        var saved = TenantBrandingJson.Parse(h.Tenant.Branding);
        Assert.DoesNotContain("<", saved.FontFamily);
        Assert.DoesNotContain(">", saved.FontFamily);
    }

    [Fact]
    public async Task Save_rejects_a_non_http_background_image_url()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<BrandingConfigInvalidException>(() =>
            h.Service.SaveAsync(Tenant, new BrandingInput
            {
                PrimaryColor = "#5b6df5", ButtonTextColor = "#fff",
                BackgroundImageUrl = "javascript:alert(1)"
            }, AuditContext.System));
    }

    [Fact]
    public async Task Save_requires_an_image_url_when_image_background_on_a_split_layout()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<BrandingConfigInvalidException>(() =>
            h.Service.SaveAsync(Tenant, new BrandingInput
            {
                PrimaryColor = "#5b6df5", ButtonTextColor = "#fff",
                Layout = Core.Enums.BrandingLayout.FormRight,
                Background = Core.Enums.BrandingBackground.Image,
                BackgroundImageUrl = null
            }, AuditContext.System));
    }

    [Fact]
    public async Task Save_sanitizes_position_clamps_numbers_and_normalizes_bullets()
    {
        var h = new Harness();
        await h.Service.SaveAsync(Tenant, new BrandingInput
        {
            PrimaryColor = "#5b6df5", ButtonTextColor = "#fff",
            BackgroundPosition = "url(evil)",
            OverlayOpacity = 500,
            CornerRadius = -4,
            FeatureBullets = new List<string> { "  one  ", "", "two", "three", "four", "five", "six", "seven" }
        }, AuditContext.System);

        var saved = TenantBrandingJson.Parse(h.Tenant.Branding);
        Assert.Equal("center", saved.BackgroundPosition);   // unsafe value rejected -> default
        Assert.Equal(100, saved.OverlayOpacity);            // clamped
        Assert.Equal(0, saved.CornerRadius);                // clamped
        Assert.Equal(6, saved.FeatureBullets.Count);        // blanks dropped, capped at 6
        Assert.Equal("one", saved.FeatureBullets[0]);       // trimmed
    }

    [Fact]
    public async Task Save_accepts_an_uploaded_asset_ref_as_the_logo()
    {
        var h = new Harness();
        await h.Service.SaveAsync(Tenant, new BrandingInput
        {
            PrimaryColor = "#5b6df5", ButtonTextColor = "#fff",
            LogoUrl = "/branding/asset/" + Guid.NewGuid()
        }, AuditContext.System); // no throw

        var saved = TenantBrandingJson.Parse(h.Tenant.Branding);
        Assert.StartsWith("/branding/asset/", saved.LogoUrl);
    }

    [Fact]
    public async Task SaveImage_stores_bytes_replaces_prior_and_returns_relative_url()
    {
        var h = new Harness();
        var url1 = await h.Service.SaveImageAsync(Tenant, "logo", new byte[] { 1, 2, 3 }, "image/png", AuditContext.System);
        var url2 = await h.Service.SaveImageAsync(Tenant, "logo", new byte[] { 4, 5 }, "image/png", AuditContext.System);

        Assert.StartsWith("/branding/asset/", url1);
        Assert.Single(h.Assets.Items);                       // prior logo replaced
        Assert.Equal(new byte[] { 4, 5 }, h.Assets.Items[0].Data);
        Assert.EndsWith(h.Assets.Items[0].Id.ToString(), url2);
        Assert.Contains("tenant.branding_image_uploaded", h.Audit.Events);
    }

    [Fact]
    public async Task SaveImage_rejects_an_unsupported_content_type()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<BrandingConfigInvalidException>(() =>
            h.Service.SaveImageAsync(Tenant, "logo", new byte[] { 1 }, "application/pdf", AuditContext.System));
    }

    [Fact]
    public async Task SaveImage_rejects_an_unknown_kind()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<BrandingConfigInvalidException>(() =>
            h.Service.SaveImageAsync(Tenant, "banner", new byte[] { 1 }, "image/png", AuditContext.System));
    }

    [Fact]
    public async Task SetCustomDomain_normalizes_and_persists()
    {
        var h = new Harness();
        await h.Service.SetCustomDomainAsync(Tenant, "https://Auth.Acme.com/", AuditContext.System);
        Assert.Equal("auth.acme.com", h.Tenant.CustomDomain);
        Assert.Contains("tenant.custom_domain_updated", h.Audit.Events);
    }

    [Fact]
    public async Task SetCustomDomain_clears_on_blank()
    {
        var h = new Harness();
        h.Tenant.CustomDomain = "auth.acme.com";
        await h.Service.SetCustomDomainAsync(Tenant, "   ", AuditContext.System);
        Assert.Null(h.Tenant.CustomDomain);
    }

    [Fact]
    public async Task SetCustomDomain_rejects_an_invalid_host()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<BrandingConfigInvalidException>(() =>
            h.Service.SetCustomDomainAsync(Tenant, "not a domain", AuditContext.System));
    }

    [Fact]
    public async Task SetCustomDomain_rejects_a_domain_owned_by_another_tenant()
    {
        var h = new Harness();
        var other = new Tenant { Id = Guid.NewGuid(), Name = "Other", Slug = "other", CustomDomain = "auth.acme.com" };
        h.Repo.Store[other.Id] = other;

        await Assert.ThrowsAsync<BrandingConfigInvalidException>(() =>
            h.Service.SetCustomDomainAsync(Tenant, "auth.acme.com", AuditContext.System));
    }

    [Fact]
    public async Task SetCustomDomain_allows_keeping_the_tenants_own_domain()
    {
        var h = new Harness();
        h.Tenant.CustomDomain = "auth.acme.com";
        await h.Service.SetCustomDomainAsync(Tenant, "auth.acme.com", AuditContext.System); // no throw
        Assert.Equal("auth.acme.com", h.Tenant.CustomDomain);
    }

    private sealed class Harness
    {
        public readonly FakeTenantRepo Repo = new();
        public readonly FakeBrandingAssetRepo Assets = new();
        public readonly RecordingAudit Audit = new();
        public readonly BrandingService Service;
        public readonly Tenant Tenant;

        public Harness()
        {
            Tenant = new Tenant { Id = BrandingServiceTests.Tenant, Name = "Acme", Slug = "acme" };
            Repo.Store[Tenant.Id] = Tenant;
            Service = new BrandingService(Repo, Assets, Audit);
        }
    }

    private sealed class FakeBrandingAssetRepo : IBrandingAssetRepository
    {
        public readonly List<BrandingAsset> Items = new();
        public Task<BrandingAsset?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(a => a.Id == id));
        public Task AddAsync(BrandingAsset asset, CancellationToken ct = default)
        {
            if (asset.Id == Guid.Empty) asset.Id = Guid.NewGuid();
            Items.Add(asset);
            return Task.CompletedTask;
        }
        public Task DeleteByKindAsync(Guid tenantId, string kind, CancellationToken ct = default)
        {
            Items.RemoveAll(a => a.TenantId == tenantId && a.Kind == kind);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeTenantRepo : ITenantRepository
    {
        public readonly Dictionary<Guid, Tenant> Store = new();
        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Store.GetValueOrDefault(id));
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Store.Values.FirstOrDefault(t => t.Slug == slug));
        public Task<Tenant?> GetByCustomDomainOrNullAsync(string host, CancellationToken ct = default)
            => Task.FromResult(Store.Values.FirstOrDefault(t => t.CustomDomain == host));
        public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(Store.Values.ToList());
        public Task<IReadOnlyList<Tenant>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Tenant>>(Store.Values.Where(t => t.OrganizationId == organizationId).ToList());
        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default)
            => Task.FromResult(Store.Values.Any(t => t.Slug == slug));
        public Task AddAsync(Tenant tenant, CancellationToken ct = default) { Store[tenant.Id] = tenant; return Task.CompletedTask; }
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) { Store[tenant.Id] = tenant; return Task.CompletedTask; }
    }

    private sealed class RecordingAudit : IAuditLogger
    {
        public readonly List<string> Events = new();
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
            Guid? resourceId = null, string result = "success", object? metadata = null, bool publishEvent = true, CancellationToken ct = default)
        { Events.Add(@event); return Task.CompletedTask; }
    }
}
