using System.Text.RegularExpressions;
using Authly.Core.Branding;
using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Audit;
using Authly.Modules.Common;

namespace Authly.Modules.Branding;

/// <inheritdoc />
public sealed partial class BrandingService : IBrandingService
{
    private readonly ITenantRepository _tenants;
    private readonly IBrandingAssetRepository _assets;
    private readonly IAuditLogger _audit;

    /// <summary>Image MIME types we accept for uploaded logo/background assets.</summary>
    private static readonly HashSet<string> AllowedImageTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/gif", "image/webp", "image/svg+xml", "image/x-icon", "image/vnd.microsoft.icon"
    };

    public BrandingService(ITenantRepository tenants, IBrandingAssetRepository assets, IAuditLogger audit)
    {
        _tenants = tenants;
        _assets = assets;
        _audit = audit;
    }

    public async Task<TenantBranding> GetAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        return tenant is null ? TenantBranding.Default : TenantBrandingJson.Parse(tenant.Branding);
    }

    public async Task<string?> GetCustomDomainAsync(Guid tenantId, CancellationToken ct = default)
        => (await _tenants.GetByIdAsync(tenantId, ct))?.CustomDomain;

    public async Task SaveAsync(Guid tenantId, BrandingInput input, AuditContext actor, CancellationToken ct = default)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found.");

        var branding = new TenantBranding
        {
            // identity
            LogoUrl = NormalizeOptional(input.LogoUrl),
            BrandName = NormalizeOptional(input.BrandName),
            PrimaryColor = ValidateColor(input.PrimaryColor, "primary color"),
            ButtonTextColor = ValidateColor(input.ButtonTextColor, "button text color"),
            FontFamily = string.IsNullOrWhiteSpace(input.FontFamily)
                ? TenantBranding.Default.FontFamily
                : SanitizeFont(input.FontFamily),
            DarkMode = input.DarkMode,

            // layout
            Layout = input.Layout,

            // background
            Background = input.Background,
            BackgroundColor = ValidateColor(input.BackgroundColor, "background color"),
            GradientFrom = ValidateColor(input.GradientFrom, "gradient start color"),
            GradientTo = ValidateColor(input.GradientTo, "gradient end color"),
            BackgroundImageUrl = NormalizeOptional(input.BackgroundImageUrl),
            BackgroundFit = input.BackgroundFit,
            BackgroundPosition = SanitizePosition(input.BackgroundPosition),
            OverlayOpacity = Math.Clamp(input.OverlayOpacity, 0, 100),

            // text
            Heading = NormalizeOptional(input.Heading) ?? TenantBranding.Default.Heading,
            Subtitle = NormalizeOptional(input.Subtitle) ?? TenantBranding.Default.Subtitle,
            HeadingSize = input.HeadingSize,
            Tagline = NormalizeOptional(input.Tagline),
            FeatureBullets = NormalizeBullets(input.FeatureBullets),
            FooterText = NormalizeOptional(input.FooterText),

            // card / shape
            CardStyle = input.CardStyle,
            CardShadow = input.CardShadow,
            CornerRadius = Math.Clamp(input.CornerRadius, 0, 16)
        };

        if (branding.LogoUrl is not null && !IsAllowedImageRef(branding.LogoUrl))
            throw new BrandingConfigInvalidException("The logo must be an absolute http(s) URL or an uploaded image.");

        if (branding.BackgroundImageUrl is not null && !IsAllowedImageRef(branding.BackgroundImageUrl))
            throw new BrandingConfigInvalidException("The background image must be an absolute http(s) URL or an uploaded image.");

        if (branding.Background == Core.Enums.BrandingBackground.Image
            && branding.Layout != Core.Enums.BrandingLayout.CenteredPlain
            && branding.BackgroundImageUrl is null)
            throw new BrandingConfigInvalidException("Choose a background image URL, or pick a solid/gradient background.");

        tenant.Branding = TenantBrandingJson.Serialize(branding);
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _tenants.UpdateAsync(tenant, ct);

        await _audit.LogAsync("tenant.branding_updated", actor, tenantId: tenant.Id,
            resourceType: "tenant", resourceId: tenant.Id,
            metadata: new { branding.Layout, branding.DarkMode, hasLogo = branding.LogoUrl is not null }, ct: ct);
    }

    public async Task<string> SaveImageAsync(Guid tenantId, string kind, byte[] data, string contentType,
        AuditContext actor, CancellationToken ct = default)
    {
        var normalizedKind = kind?.Trim().ToLowerInvariant();
        if (normalizedKind is not ("logo" or "background"))
            throw new BrandingConfigInvalidException("Unsupported image kind.");

        if (data is null || data.Length == 0)
            throw new BrandingConfigInvalidException("The uploaded file is empty.");

        var type = contentType?.Trim().ToLowerInvariant() ?? "";
        if (!AllowedImageTypes.Contains(type))
            throw new BrandingConfigInvalidException("Use a PNG, JPEG, GIF, WebP, SVG or ICO image.");

        _ = await _tenants.GetByIdAsync(tenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found.");

        // One current asset per kind — drop the previous upload so bytes don't accumulate.
        await _assets.DeleteByKindAsync(tenantId, normalizedKind, ct);

        var asset = new BrandingAsset
        {
            TenantId = tenantId,
            Kind = normalizedKind,
            ContentType = type,
            Data = data
        };
        await _assets.AddAsync(asset, ct);

        await _audit.LogAsync("tenant.branding_image_uploaded", actor, tenantId: tenantId,
            resourceType: "tenant", resourceId: tenantId,
            metadata: new { kind = normalizedKind, contentType = type, bytes = data.Length }, ct: ct);

        return $"/branding/asset/{asset.Id}";
    }

    public async Task SetCustomDomainAsync(Guid tenantId, string? domain, AuditContext actor, CancellationToken ct = default)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct)
            ?? throw new KeyNotFoundException($"Tenant {tenantId} not found.");

        var normalized = NormalizeDomain(domain);
        if (normalized is not null)
        {
            if (!IsValidHost(normalized))
                throw new BrandingConfigInvalidException("Enter a valid hostname, e.g. auth.yourcompany.com.");

            // A domain resolves to exactly one tenant — reject if another tenant already owns it.
            var owner = await _tenants.GetByCustomDomainOrNullAsync(normalized, ct);
            if (owner is not null && owner.Id != tenantId)
                throw new BrandingConfigInvalidException("That domain is already in use by another organization.");
        }

        tenant.CustomDomain = normalized;
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _tenants.UpdateAsync(tenant, ct);

        await _audit.LogAsync("tenant.custom_domain_updated", actor, tenantId: tenant.Id,
            resourceType: "tenant", resourceId: tenant.Id,
            metadata: new { domain = normalized }, ct: ct);
    }

    // --- validation helpers -------------------------------------------------

    /// <summary>Accepts #rgb / #rrggbb (case-insensitive). Returns the lower-cased hex.</summary>
    private static string ValidateColor(string? value, string field)
    {
        var v = value?.Trim() ?? "";
        if (!HexColor().IsMatch(v))
            throw new BrandingConfigInvalidException($"The {field} must be a hex color like #5b6df5.");
        return v.ToLowerInvariant();
    }

    /// <summary>Strips characters that could break out of a CSS font-family declaration.</summary>
    private static string SanitizeFont(string value)
    {
        var cleaned = FontDisallowed().Replace(value.Trim(), "");
        return string.IsNullOrWhiteSpace(cleaned) ? TenantBranding.Default.FontFamily : cleaned;
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Keeps only a safe CSS <c>background-position</c> value (keywords / percentages / px).</summary>
    private static string SanitizePosition(string? value)
    {
        var v = value?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(v) || !Position().IsMatch(v)) return "center";
        return v;
    }

    /// <summary>Trims, drops blanks, caps each bullet length, and limits the list to 6 items.</summary>
    private static List<string> NormalizeBullets(IEnumerable<string>? bullets)
        => (bullets ?? Enumerable.Empty<string>())
            .Select(b => b?.Trim() ?? "")
            .Where(b => b.Length > 0)
            .Select(b => b.Length > 80 ? b[..80] : b)
            .Take(6)
            .ToList();

    private static string? NormalizeDomain(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var v = value.Trim().ToLowerInvariant();
        // Tolerate a pasted URL — keep only the host.
        if (v.Contains("://") && Uri.TryCreate(v, UriKind.Absolute, out var uri)) v = uri.Host;
        return v.TrimEnd('/');
    }

    private static bool IsHttpUrl(string url)
        => Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    /// <summary>Accepts an absolute http(s) URL or an app-relative uploaded-asset ref (/branding/asset/{id}).</summary>
    private static bool IsAllowedImageRef(string url)
        => IsHttpUrl(url) || url.StartsWith("/branding/asset/", StringComparison.Ordinal);

    private static bool IsValidHost(string host) => Host().IsMatch(host);

    [GeneratedRegex("^#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})$")]
    private static partial Regex HexColor();

    // CSS font lists are letters/digits/spaces/commas/hyphens and quotes only.
    [GeneratedRegex("[^a-zA-Z0-9 ,\\-'\"]")]
    private static partial Regex FontDisallowed();

    // CSS background-position: 1–2 tokens of keyword | percentage | px length.
    [GeneratedRegex("^(left|right|top|bottom|center|\\d{1,3}%|\\d{1,4}px)( (left|right|top|bottom|center|\\d{1,3}%|\\d{1,4}px))?$")]
    private static partial Regex Position();

    // A conservative DNS hostname (labels of letters/digits/hyphens, at least one dot).
    [GeneratedRegex("^(?=.{1,253}$)(?!-)[a-z0-9-]{1,63}(?<!-)(\\.(?!-)[a-z0-9-]{1,63}(?<!-))+$")]
    private static partial Regex Host();
}
