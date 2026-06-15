using Authly.Core.Interfaces;
using Authly.Core.Messaging;

namespace Authly.Web.Infrastructure.Messaging;

/// <summary>
/// Hangfire-invoked job that delivers a queued <see cref="EmailMessage"/> via the registered
/// <see cref="IEmailSender"/>. Kept as a thin, serializable entry point so Hangfire stores
/// only the message payload and resolves the sender from DI at execution time.
/// </summary>
public sealed class EmailDispatchJob
{
    private readonly IEmailSender _sender;

    public EmailDispatchJob(IEmailSender sender) => _sender = sender;

    public Task DispatchAsync(EmailMessage message) => _sender.SendAsync(message);
}
