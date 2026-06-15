using System.Text.Json;
using Authly.Core.Enums;

namespace Authly.Core.Branding;

/// <summary>
/// Pure (de)serialization between <see cref="TenantBranding"/> and the JSON stored in
/// <c>tenants.branding</c>. Lives in Core so both the Branding module service and the web
/// presentation layer parse the column identically. Unknown / malformed input degrades to
/// <see cref="TenantBranding.Default"/> values rather than throwing — a bad column must never
/// take a login page down.
/// </summary>
public static class TenantBrandingJson
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    /// <summary>Parses the stored JSON; returns the platform default on null/blank/malformed input.</summary>
    public static TenantBranding Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return TenantBranding.Default;

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return TenantBranding.Default;

            var b = new TenantBranding();
            if (TryString(root, "logo_url", out var logo)) b.LogoUrl = logo;
            if (TryString(root, "primary_color", out var primary) && !string.IsNullOrWhiteSpace(primary)) b.PrimaryColor = primary;
            if (TryString(root, "button_text_color", out var btn) && !string.IsNullOrWhiteSpace(btn)) b.ButtonTextColor = btn;
            if (TryString(root, "font_family", out var font) && !string.IsNullOrWhiteSpace(font)) b.FontFamily = font;
            if (TryString(root, "layout", out var layout)
                && Enum.TryParse<BrandingLayout>(layout, ignoreCase: true, out var parsed))
                b.Layout = parsed;
            if (root.TryGetProperty("dark_mode", out var dark)
                && (dark.ValueKind == JsonValueKind.True || dark.ValueKind == JsonValueKind.False))
                b.DarkMode = dark.GetBoolean();
            if (TryString(root, "tagline", out var tagline)) b.Tagline = tagline;
            return b;
        }
        catch (JsonException)
        {
            return TenantBranding.Default;
        }
    }

    /// <summary>Serializes branding to the canonical snake_case JSON for storage.</summary>
    public static string Serialize(TenantBranding b) => JsonSerializer.Serialize(new Dictionary<string, object?>
    {
        ["logo_url"] = b.LogoUrl,
        ["primary_color"] = b.PrimaryColor,
        ["button_text_color"] = b.ButtonTextColor,
        ["font_family"] = b.FontFamily,
        ["layout"] = b.Layout.ToString().ToLowerInvariant(),
        ["dark_mode"] = b.DarkMode,
        ["tagline"] = b.Tagline
    }, Options);

    private static bool TryString(JsonElement root, string name, out string? value)
    {
        if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.String)
        {
            value = el.GetString();
            return true;
        }
        value = null;
        return false;
    }
}
