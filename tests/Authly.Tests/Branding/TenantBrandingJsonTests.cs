using Authly.Core.Branding;
using Authly.Core.Enums;

namespace Authly.Tests.Branding;

public class TenantBrandingJsonTests
{
    [Fact]
    public void Parse_returns_default_for_null_blank_or_empty_object()
    {
        Assert.True(TenantBrandingJson.Parse(null).IsDefault);
        Assert.True(TenantBrandingJson.Parse("").IsDefault);
        Assert.True(TenantBrandingJson.Parse("{}").IsDefault);
    }

    [Fact]
    public void Parse_returns_default_for_malformed_json()
        => Assert.True(TenantBrandingJson.Parse("{not valid").IsDefault);

    [Fact]
    public void Round_trip_preserves_all_fields()
    {
        var original = new TenantBranding
        {
            LogoUrl = "https://cdn.acme.com/logo.svg",
            PrimaryColor = "#112233",
            ButtonTextColor = "#fafafa",
            FontFamily = "Roboto, sans-serif",
            DarkMode = true,
            Layout = BrandingLayout.FormLeft,
            Background = BrandingBackground.Image,
            GradientFrom = "#010203",
            GradientTo = "#040506",
            BackgroundImageUrl = "https://cdn.acme.com/bg.jpg",
            BackgroundFit = BackgroundFit.Contain,
            BackgroundPosition = "top",
            OverlayOpacity = 60,
            Heading = "Sign in to Acme",
            Subtitle = "Your workspace awaits",
            HeadingSize = HeadingSize.Large,
            Tagline = "Welcome to Acme",
            FeatureBullets = new List<string> { "Fast", "Secure" },
            FooterText = "© Acme",
            CardStyle = CardStyle.Glass,
            CardShadow = CardShadow.Strong,
            CornerRadius = 12
        };

        var parsed = TenantBrandingJson.Parse(TenantBrandingJson.Serialize(original));

        Assert.Equal(original.LogoUrl, parsed.LogoUrl);
        Assert.Equal(original.PrimaryColor, parsed.PrimaryColor);
        Assert.Equal(original.ButtonTextColor, parsed.ButtonTextColor);
        Assert.Equal(original.FontFamily, parsed.FontFamily);
        Assert.True(parsed.DarkMode);
        Assert.Equal(BrandingLayout.FormLeft, parsed.Layout);
        Assert.Equal(BrandingBackground.Image, parsed.Background);
        Assert.Equal(original.GradientFrom, parsed.GradientFrom);
        Assert.Equal(original.GradientTo, parsed.GradientTo);
        Assert.Equal(original.BackgroundImageUrl, parsed.BackgroundImageUrl);
        Assert.Equal(BackgroundFit.Contain, parsed.BackgroundFit);
        Assert.Equal("top", parsed.BackgroundPosition);
        Assert.Equal(60, parsed.OverlayOpacity);
        Assert.Equal(original.Heading, parsed.Heading);
        Assert.Equal(original.Subtitle, parsed.Subtitle);
        Assert.Equal(HeadingSize.Large, parsed.HeadingSize);
        Assert.Equal(original.Tagline, parsed.Tagline);
        Assert.Equal(original.FeatureBullets, parsed.FeatureBullets);
        Assert.Equal(original.FooterText, parsed.FooterText);
        Assert.Equal(CardStyle.Glass, parsed.CardStyle);
        Assert.Equal(CardShadow.Strong, parsed.CardShadow);
        Assert.Equal(12, parsed.CornerRadius);
        Assert.False(parsed.IsDefault);
    }

    [Theory]
    [InlineData("centered", BrandingLayout.CenteredPlain)]
    [InlineData("split", BrandingLayout.FormRight)]
    public void Parse_maps_legacy_layout_values(string stored, BrandingLayout expected)
        => Assert.Equal(expected, TenantBrandingJson.Parse($"{{\"layout\":\"{stored}\"}}").Layout);

    [Fact]
    public void Parse_clamps_out_of_range_numbers()
    {
        var b = TenantBrandingJson.Parse("{\"overlay_opacity\":250,\"corner_radius\":99}");
        Assert.Equal(100, b.OverlayOpacity);
        Assert.Equal(16, b.CornerRadius);
    }

    [Theory]
    [InlineData("#5b6df5", "91, 109, 245")]
    [InlineData("#fff", "255, 255, 255")]
    [InlineData("#000000", "0, 0, 0")]
    [InlineData("not-a-color", "91, 109, 245")] // falls back to platform indigo
    public void PrimaryColorRgb_converts_hex(string hex, string expected)
        => Assert.Equal(expected, new TenantBranding { PrimaryColor = hex }.PrimaryColorRgb);
}
