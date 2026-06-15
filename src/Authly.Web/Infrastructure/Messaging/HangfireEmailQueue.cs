using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Hangfire;

namespace Authly.Web.Infrastructure.Messaging;

/// <summary>
/// <see cref="IEmailQueue"/> backed by Hangfire — module services enqueue here and return
/// immediately; delivery happens out of band in <see cref="EmailDispatchJob"/>.
/// </summary>
public sealed class HangfireEmailQueue : IEmailQueue
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireEmailQueue(IBackgroundJobClient jobs) => _jobs = jobs;

    public void Queue(EmailMessage message)
        => _jobs.Enqueue<EmailDispatchJob>(job => job.DispatchAsync(message));
}
