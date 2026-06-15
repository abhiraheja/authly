using Authly.Core.Events;
using Authly.Modules.Webhooks;

namespace Authly.Tests.Webhooks;

public class WebhookSignerAndScheduleTests
{
    private static readonly DateTimeOffset T = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

    [Fact]
    public void Sign_is_deterministic_and_prefixed()
    {
        var a = WebhookSigner.Sign("secret", """{"x":1}""", T);
        var b = WebhookSigner.Sign("secret", """{"x":1}""", T);

        Assert.Equal(a, b);
        Assert.StartsWith("sha256=", a);
    }

    [Fact]
    public void Sign_changes_with_secret_body_and_timestamp()
    {
        var baseline = WebhookSigner.Sign("secret", "body", T);
        Assert.NotEqual(baseline, WebhookSigner.Sign("other", "body", T));
        Assert.NotEqual(baseline, WebhookSigner.Sign("secret", "body2", T));
        Assert.NotEqual(baseline, WebhookSigner.Sign("secret", "body", T.AddSeconds(1)));
    }

    [Fact]
    public void Verify_accepts_fresh_and_rejects_stale_or_tampered()
    {
        var sig = WebhookSigner.Sign("secret", "body", T);

        Assert.True(WebhookSigner.Verify("secret", "body", T, sig, T.AddSeconds(30), TimeSpan.FromMinutes(5)));
        // Replay protection: outside the tolerance window.
        Assert.False(WebhookSigner.Verify("secret", "body", T, sig, T.AddMinutes(10), TimeSpan.FromMinutes(5)));
        // Wrong secret.
        Assert.False(WebhookSigner.Verify("nope", "body", T, sig, T, TimeSpan.FromMinutes(5)));
    }

    [Fact]
    public void Retry_ladder_matches_the_spec_then_exhausts()
    {
        Assert.Equal(TimeSpan.FromMinutes(1), WebhookRetrySchedule.DelayAfter(1));
        Assert.Equal(TimeSpan.FromMinutes(5), WebhookRetrySchedule.DelayAfter(2));
        Assert.Equal(TimeSpan.FromMinutes(30), WebhookRetrySchedule.DelayAfter(3));
        Assert.Equal(TimeSpan.FromHours(2), WebhookRetrySchedule.DelayAfter(4));
        Assert.Equal(TimeSpan.FromHours(24), WebhookRetrySchedule.DelayAfter(5));
        Assert.Null(WebhookRetrySchedule.DelayAfter(6)); // exhausted
        Assert.Equal(6, WebhookRetrySchedule.MaxAttempts);
    }
}
