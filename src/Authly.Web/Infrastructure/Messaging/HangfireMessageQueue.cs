using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Hangfire;

namespace Authly.Web.Infrastructure.Messaging;

/// <summary>
/// <see cref="IMessageQueue"/> backed by Hangfire — module services enqueue here and return
/// immediately; delivery happens out of band in <see cref="MessageDispatchJob"/>.
/// </summary>
public sealed class HangfireMessageQueue : IMessageQueue
{
    private readonly IBackgroundJobClient _jobs;

    public HangfireMessageQueue(IBackgroundJobClient jobs) => _jobs = jobs;

    public void Enqueue(MessageSendRequest request)
        => _jobs.Enqueue<MessageDispatchJob>(job => job.DispatchAsync(request));
}
