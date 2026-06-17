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

            // identity
            if (TryString(root, "logo_url", out var logo)) b.LogoUrl = logo;
            if (TryString(root, "brand_name", out var brandName)) b.BrandName = brandName;
            if (TryNonEmpty(root, "primary_color", out var primary)) b.PrimaryColor = primary;
            if (TryNonEmpty(root, "button_text_color", out var btn)) b.ButtonTextColor = btn;
            if (TryNonEmpty(root, "font_family", out var font)) b.FontFamily = font;
            if (TryBool(root, "dark_mode", out var dark)) b.DarkMode = dark;

            // layout (with legacy back-compat: "centered" -> CenteredPlain, "split" -> FormRight)
            if (TryString(root, "layout", out var layout) && !string.IsNullOrWhiteSpace(layout))
                b.Layout = ParseLayout(layout!);

            // background
            if (TryString(root, "background", out var bg) && Enum.TryParse<BrandingBackground>(bg, true, out var pBg))
                b.Background = pBg;
            if (TryNonEmpty(root, "background_color", out var bgColor)) b.BackgroundColor = bgColor;
            if (TryNonEmpty(root, "gradient_from", out var gFrom)) b.GradientFrom = gFrom;
            if (TryNonEmpty(root, "gradient_to", out var gTo)) b.GradientTo = gTo;
            if (TryString(root, "background_image_url", out var bgImg)) b.BackgroundImageUrl = bgImg;
            if (TryString(root, "background_fit", out var fit) && Enum.TryParse<BackgroundFit>(fit, true, out var pFit))
                b.BackgroundFit = pFit;
            if (TryNonEmpty(root, "background_position", out var pos)) b.BackgroundPosition = pos;
            if (TryInt(root, "overlay_opacity", out var overlay)) b.OverlayOpacity = Math.Clamp(overlay, 0, 100);

            // text
            if (TryNonEmpty(root, "heading", out var heading)) b.Heading = heading;
            if (TryNonEmpty(root, "subtitle", out var subtitle)) b.Subtitle = subtitle;
            if (TryString(root, "heading_size", out var hs) && Enum.TryParse<HeadingSize>(hs, true, out var pHs))
                b.HeadingSize = pHs;
            if (TryString(root, "tagline", out var tagline)) b.Tagline = tagline;
            if (TryStringArray(root, "feature_bullets", out var bullets)) b.FeatureBullets = bullets;
            if (TryString(root, "footer_text", out var footer)) b.FooterText = footer;

            // card / shape
            if (TryString(root, "card_style", out var cs) && Enum.TryParse<CardStyle>(cs, true, out var pCs))
                b.CardStyle = pCs;
            if (TryString(root, "card_shadow", out var sh) && Enum.TryParse<CardShadow>(sh, true, out var pSh))
                b.CardShadow = pSh;
            if (TryInt(root, "corner_radius", out var radius)) b.CornerRadius = Math.Clamp(radius, 0, 16);

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
        ["brand_name"] = b.BrandName,
        ["primary_color"] = b.PrimaryColor,
        ["button_text_color"] = b.ButtonTextColor,
        ["font_family"] = b.FontFamily,
        ["dark_mode"] = b.DarkMode,
        ["layout"] = b.Layout.ToString().ToLowerInvariant(),
        ["background"] = b.Background.ToString().ToLowerInvariant(),
        ["background_color"] = b.BackgroundColor,
        ["gradient_from"] = b.GradientFrom,
        ["gradient_to"] = b.GradientTo,
        ["background_image_url"] = b.BackgroundImageUrl,
        ["background_fit"] = b.BackgroundFit.ToString().ToLowerInvariant(),
        ["background_position"] = b.BackgroundPosition,
        ["overlay_opacity"] = b.OverlayOpacity,
        ["heading"] = b.Heading,
        ["subtitle"] = b.Subtitle,
        ["heading_size"] = b.HeadingSize.ToString().ToLowerInvariant(),
        ["tagline"] = b.Tagline,
        ["feature_bullets"] = b.FeatureBullets,
        ["footer_text"] = b.FooterText,
        ["card_style"] = b.CardStyle.ToString().ToLowerInvariant(),
        ["card_shadow"] = b.CardShadow.ToString().ToLowerInvariant(),
        ["corner_radius"] = b.CornerRadius
    }, Options);

    private static BrandingLayout ParseLayout(string value)
    {
        var v = value.Trim().ToLowerInvariant();
        return v switch
        {
            "centered" => BrandingLayout.CenteredPlain,   // legacy value
            "split" => BrandingLayout.FormRight,          // legacy value (panel left, form right)
            _ => Enum.TryParse<BrandingLayout>(value, true, out var parsed) ? parsed : BrandingLayout.CenteredPlain
        };
    }

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

    private static bool TryNonEmpty(JsonElement root, string name, out string value)
    {
        if (TryString(root, name, out var s) && !string.IsNullOrWhiteSpace(s))
        {
            value = s!;
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryBool(JsonElement root, string name, out bool value)
    {
        if (root.TryGetProperty(name, out var el)
            && (el.ValueKind == JsonValueKind.True || el.ValueKind == JsonValueKind.False))
        {
            value = el.GetBoolean();
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryInt(JsonElement root, string name, out int value)
    {
        if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Number && el.TryGetInt32(out var n))
        {
            value = n;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryStringArray(JsonElement root, string name, out List<string> value)
    {
        value = new List<string>();
        if (root.TryGetProperty(name, out var el) && el.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in el.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) value.Add(s!);
                }
            }
            return true;
        }
        return false;
    }
}
