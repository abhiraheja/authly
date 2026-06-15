using Authly.Core.Messaging;

namespace Authly.Core.Interfaces;

/// <summary>
/// Performs the actual delivery of an <see cref="EmailMessage"/>. Phase 2 ships a stub
/// implementation that logs the payload; a real provider (SMTP / Zepto, with per-tenant
/// BYOK) replaces it in a later phase. Invoked by the background email job, not directly.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct = default);
}
