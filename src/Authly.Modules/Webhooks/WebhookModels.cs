namespace Authly.Modules.Webhooks;

/// <summary>Admin form payload for creating/updating a webhook endpoint.</summary>
public sealed class WebhookEndpointInput
{
    public Guid? Id { get; set; }
    public string Url { get; set; } = "";

    /// <summary>Subscribed event names; the wildcard <c>*</c> matches everything.</summary>
    public string[] Events { get; set; } = Array.Empty<string>();

    /// <summary>New signing secret; blank on edit means keep the existing one.</summary>
    public string? Secret { get; set; }

    public bool IsActive { get; set; } = true;
}

/// <summary>
/// The fixed exponential-backoff ladder for webhook delivery retries (§4.12): the initial attempt,
/// then 1m → 5m → 30m → 2h → 24h. After the final retry, the delivery is marked failed.
/// </summary>
public static class WebhookRetrySchedule
{
    private static readonly TimeSpan[] Ladder =
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(24)
    };

    /// <summary>Maximum number of send attempts (1 initial + 5 retries).</summary>
    public static int MaxAttempts => Ladder.Length + 1;

    /// <summary>
    /// Delay before the next attempt given how many attempts have already failed, or null when the
    /// ladder is exhausted (the delivery should be marked permanently failed).
    /// </summary>
    public static TimeSpan? DelayAfter(int attemptsMade)
        => attemptsMade >= 1 && attemptsMade <= Ladder.Length ? Ladder[attemptsMade - 1] : null;
}

/// <summary>Thrown when an endpoint config is missing required fields.</summary>
public sealed class WebhookConfigInvalidException : Exception
{
    public WebhookConfigInvalidException(string message) : base(message) { }
}
