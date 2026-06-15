using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Microsoft.Extensions.Logging;

namespace Authly.Infrastructure.Messaging;

/// <summary>
/// Development email sender that does not deliver anything — it logs the message so flows
/// (verification, password reset) can be exercised locally. Recipient and subject log at
/// Information; the body (which may contain a verification/reset link, i.e. a secret) logs
/// only at Debug, so production-level logging never records the link. Replaced by a real
/// BYOK provider in a later phase.
/// </summary>
public sealed class StubEmailSender : IEmailSender
{
    private readonly ILogger<StubEmailSender> _logger;

    public StubEmailSender(ILogger<StubEmailSender> logger) => _logger = logger;

    public Task SendAsync(EmailMessage message, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[STUB EMAIL] To {ToEmail} | Subject: {Subject} (no real provider configured — Phase 2 stub)",
            message.ToEmail, message.Subject);

        // Body may embed a one-time link/token — keep it out of Information-level logs.
        _logger.LogDebug("[STUB EMAIL] Body for {ToEmail}:\n{TextBody}", message.ToEmail, message.TextBody);

        return Task.CompletedTask;
    }
}
