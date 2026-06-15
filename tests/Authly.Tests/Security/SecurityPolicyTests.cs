using Authly.Core.Entities;
using Authly.Modules.Security;

namespace Authly.Tests.Security;

public class SecurityPolicyTests
{
    // --- LockoutPolicy (exponential backoff) --------------------------------

    [Theory]
    [InlineData(4, 5, 0)]      // below threshold → no lock
    [InlineData(5, 5, 1)]      // at threshold → 1 min
    [InlineData(6, 5, 2)]      // +1 → 2 min
    [InlineData(7, 5, 4)]      // +2 → 4 min
    public void Lockout_backoff_grows_then_caps(int failures, int threshold, int expectedMinutes)
        => Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), LockoutPolicy.DurationFor(failures, threshold));

    [Fact]
    public void Lockout_duration_is_capped_at_one_hour()
        => Assert.Equal(TimeSpan.FromHours(1), LockoutPolicy.DurationFor(100, 5));

    // --- BlockListPolicy: email --------------------------------------------

    [Fact]
    public void Email_block_matches_domain_and_disposable()
    {
        Assert.True(BlockListPolicy.IsEmailBlocked("a@spam.example", new[] { "spam.example" }, false));
        Assert.True(BlockListPolicy.IsEmailBlocked("a@MAILINATOR.com", Array.Empty<string>(), blockDisposable: true));
        Assert.False(BlockListPolicy.IsEmailBlocked("a@good.com", new[] { "spam.example" }, true));
    }

    // --- BlockListPolicy: IP / CIDR ----------------------------------------

    [Fact]
    public void Ip_block_matches_single_and_cidr()
    {
        Assert.True(BlockListPolicy.IsIpBlocked("203.0.113.5", new[] { "203.0.113.0/24" }));
        Assert.True(BlockListPolicy.IsIpBlocked("198.51.100.7", new[] { "198.51.100.7" }));
        Assert.False(BlockListPolicy.IsIpBlocked("8.8.8.8", new[] { "203.0.113.0/24" }));
    }

    [Fact]
    public void Allowlist_empty_permits_all_but_non_empty_restricts()
    {
        Assert.True(BlockListPolicy.IsIpAllowed("8.8.8.8", Array.Empty<string>()));
        Assert.True(BlockListPolicy.IsIpAllowed("10.0.0.5", new[] { "10.0.0.0/8" }));
        Assert.False(BlockListPolicy.IsIpAllowed("8.8.8.8", new[] { "10.0.0.0/8" }));
    }

    [Fact]
    public void Country_block_is_case_insensitive()
    {
        Assert.True(BlockListPolicy.IsCountryBlocked("kp", new[] { "KP" }));
        Assert.False(BlockListPolicy.IsCountryBlocked("US", new[] { "KP" }));
    }

    // --- SuspiciousLoginDetector -------------------------------------------

    [Fact]
    public void First_login_is_never_suspicious()
        => Assert.False(SuspiciousLoginDetector.IsNewContext("1.1.1.1", "ua", Successes(("1.1.1.1", "ua"))));

    [Fact]
    public void Known_ip_or_device_is_not_suspicious()
        => Assert.False(SuspiciousLoginDetector.IsNewContext("1.1.1.1", "newua",
            Successes(("1.1.1.1", "oldua"), ("1.1.1.1", "newua"))));  // ip seen before

    [Fact]
    public void New_ip_and_new_device_is_suspicious()
        => Assert.True(SuspiciousLoginDetector.IsNewContext("9.9.9.9", "newua",
            Successes(("1.1.1.1", "oldua"), ("9.9.9.9", "newua"))));  // current is the only 9.9.9.9/newua

    private static IReadOnlyList<LoginHistory> Successes(params (string Ip, string Ua)[] entries)
        => entries.Select(e => new LoginHistory { Result = "success", IpAddress = e.Ip, UserAgent = e.Ua }).ToList();
}
