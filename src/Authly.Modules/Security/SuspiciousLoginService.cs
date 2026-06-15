using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Messaging;

namespace Authly.Modules.Security;

/// <summary>Pure detection of a login from a new device + new location.</summary>
public static class SuspiciousLoginDetector
{
    /// <summary>
    /// True when the current login's IP and user-agent are BOTH unseen among the user's prior
    /// successful logins. The current login is expected to be present once in <paramref name="successes"/>;
    /// a first/only login is never suspicious.
    /// </summary>
    public static bool IsNewContext(string? currentIp, string? currentUa, IReadOnlyList<LoginHistory> successes)
    {
        if (successes.Count <= 1) return false; // first successful login — nothing to compare to

        var ipSeenBefore = successes.Count(h => h.IpAddress == currentIp) > 1;
        var uaSeenBefore = successes.Count(h => h.UserAgent == currentUa) > 1;
        return !ipSeenBefore && !uaSeenBefore;
    }
}

/// <summary>Evaluates a just-completed login for anomalies and alerts the user. Invoked from a Hangfire job.</summary>
public interface ISuspiciousLoginService
{
    Task EvaluateAsync(Guid tenantId, Guid userId, RequestInfo info, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class SuspiciousLoginService : ISuspiciousLoginService
{
    private readonly ILoginHistoryRepository _history;
    private readonly IUserRepository _users;
    private readonly IMessageQueue _messages;
    private readonly IAuditLogger _audit;

    public SuspiciousLoginService(ILoginHistoryRepository history, IUserRepository users,
        IMessageQueue messages, IAuditLogger audit)
    {
        _history = history;
        _users = users;
        _messages = messages;
        _audit = audit;
    }

    public async Task EvaluateAsync(Guid tenantId, Guid userId, RequestInfo info, CancellationToken ct = default)
    {
        var recent = await _history.ListForUserAsync(tenantId, userId, 50, ct);
        var successes = recent.Where(h => h.Result == "success").ToList();
        if (!SuspiciousLoginDetector.IsNewContext(info.IpAddress, info.UserAgent, successes))
            return;

        var user = await _users.GetByIdAsync(tenantId, userId, ct);
        if (user is null) return;

        _messages.Enqueue(new MessageSendRequest(tenantId, MessageTemplateKeys.SecurityAlert,
            MessageChannel.Email, user.Email, new Dictionary<string, string>
            {
                ["user_name"] = string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName!,
                ["message"] = $"We noticed a sign-in from a new device or location" +
                              (string.IsNullOrEmpty(info.IpAddress) ? "." : $" (IP {info.IpAddress}).")
            }));

        await _audit.LogAsync("user.suspicious_login", new AuditContext(userId, "user", info.IpAddress, info.UserAgent),
            tenantId, "user", userId, result: "flagged", ct: ct);
    }
}
