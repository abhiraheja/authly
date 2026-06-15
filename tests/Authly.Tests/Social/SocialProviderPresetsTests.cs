using Authly.Modules.Social;

namespace Authly.Tests.Social;

public class SocialProviderPresetsTests
{
    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    [InlineData("microsoft")]
    [InlineData("facebook")]
    public void Known_providers_have_complete_endpoints(string key)
    {
        Assert.True(SocialProviderPresets.IsKnown(key));
        var p = SocialProviderPresets.Find(key)!;
        Assert.False(string.IsNullOrWhiteSpace(p.AuthorizationEndpoint));
        Assert.False(string.IsNullOrWhiteSpace(p.TokenEndpoint));
        Assert.False(string.IsNullOrWhiteSpace(p.UserInfoEndpoint));
        Assert.NotEmpty(p.DefaultScopes);
        Assert.False(string.IsNullOrWhiteSpace(p.IdField));
    }

    [Fact]
    public void Custom_is_not_a_known_preset()
    {
        Assert.False(SocialProviderPresets.IsKnown("custom"));
        Assert.Null(SocialProviderPresets.Find("custom"));
    }

    [Fact]
    public void Google_uses_oidc_sub_and_email_verified()
    {
        var g = SocialProviderPresets.Find("google")!;
        Assert.Equal("sub", g.IdField);
        Assert.Equal("email_verified", g.EmailVerifiedField);
    }
}
