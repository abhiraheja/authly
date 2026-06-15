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
            Layout = BrandingLayout.Split,
            DarkMode = true,
            Tagline = "Welcome to Acme"
        };

        var parsed = TenantBrandingJson.Parse(TenantBrandingJson.Serialize(original));

        Assert.Equal(original.LogoUrl, parsed.LogoUrl);
        Assert.Equal(original.PrimaryColor, parsed.PrimaryColor);
        Assert.Equal(original.ButtonTextColor, parsed.ButtonTextColor);
        Assert.Equal(original.FontFamily, parsed.FontFamily);
        Assert.Equal(BrandingLayout.Split, parsed.Layout);
        Assert.True(parsed.DarkMode);
        Assert.Equal(original.Tagline, parsed.Tagline);
        Assert.False(parsed.IsDefault);
    }

    [Theory]
    [InlineData("#5b6df5", "91, 109, 245")]
    [InlineData("#fff", "255, 255, 255")]
    [InlineData("#000000", "0, 0, 0")]
    [InlineData("not-a-color", "91, 109, 245")] // falls back to platform indigo
    public void PrimaryColorRgb_converts_hex(string hex, string expected)
        => Assert.Equal(expected, new TenantBranding { PrimaryColor = hex }.PrimaryColorRgb);
}
