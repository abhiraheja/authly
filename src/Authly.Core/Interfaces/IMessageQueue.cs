using Authly.Core.Messaging;

namespace Authly.Core.Interfaces;

/// <summary>
/// Enqueues a <see cref="MessageSendRequest"/> for out-of-band delivery so request handling never
/// blocks on provider I/O. Implemented over Hangfire in the composition root; the queued job
/// resolves the messaging service and delivers. Module services depend only on this.
/// </summary>
public interface IMessageQueue
{
    void Enqueue(MessageSendRequest request);
}
