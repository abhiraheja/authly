using Authly.Core.Messaging;

namespace Authly.Core.Interfaces;

/// <summary>
/// Queues an <see cref="EmailMessage"/> for out-of-band delivery so request handling never
/// blocks on email I/O. Implemented over Hangfire in the composition root; the queued job
/// resolves an <see cref="IEmailSender"/> and delivers. Module services depend only on this.
/// </summary>
public interface IEmailQueue
{
    void Queue(EmailMessage message);
}
