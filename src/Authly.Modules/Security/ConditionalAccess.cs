using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Modules.Common;
using Authly.Modules.Devices;

namespace Authly.Modules.Security;

/// <summary>
/// What a tenant's conditional-access policy does when a risk signal fires. Ordered by severity
/// (Allow &lt; RequireMfa &lt; Block) so the most restrictive matching rule wins.
/// </summary>
public enum ConditionalAction
{
    Allow = 0,
    RequireMfa = 1,
    Block = 2
}

/// <summary>The evaluated action for a login attempt, plus the signal that triggered it (for audit).</summary>
public sealed record ConditionalAccessDecision(ConditionalAction Action, string? Reason)
{
    public static readonly ConditionalAccessDecision Allow = new(ConditionalAction.Allow, null);
}

/// <summary>
/// Risk-based access: evaluates the login context (new device/location, unverified email) against
/// the tenant's policy and returns whether to allow, step up to MFA, or block. Turns Phase 12's
/// suspicious-login <em>detection</em> into <em>enforcement</em>.
/// </summary>
public interface IConditionalAccessService
{
    Task<ConditionalAccessDecision> EvaluateAsync(Guid tenantId, User user, RequestInfo info, CancellationToken ct = default);
}

/// <inheritdoc />
public sealed class ConditionalAccessService : IConditionalAccessService
{
    private readonly ISecuritySettingsService _settings;
    private readonly ILoginHistoryRepository _history;
    private readonly IUserDeviceRepository _devices;

    public ConditionalAccessService(ISecuritySettingsService settings, ILoginHistoryRepository history,
        IUserDeviceRepository devices)
    {
        _settings = settings;
        _history = history;
        _devices = devices;
    }

    public async Task<ConditionalAccessDecision> EvaluateAsync(Guid tenantId, User user, RequestInfo info, CancellationToken ct = default)
    {
        var s = await _settings.GetAsync(tenantId, ct);
        if (!s.ConditionalAccessEnabled) return ConditionalAccessDecision.Allow;

        var action = ConditionalAction.Allow;
        string? reason = null;
        void Consider(ConditionalAction candidate, string r)
        {
            if (candidate > action) { action = candidate; reason = r; }
        }

        // New device/location: reuse the Phase 12 detector. The current login is already recorded
        // once in history, so the detector's "seen more than once" rule compares against priors.
        if (s.NewDeviceAction != ConditionalAction.Allow)
        {
            var recent = await _history.ListForUserAsync(tenantId, user.Id, 50, ct);
            var successes = recent.Where(h => h.Result == "success").ToList();
            if (SuspiciousLoginDetector.IsNewContext(info.IpAddress, info.UserAgent, successes))
            {
                // A device the user has explicitly trusted is exempt from the new-device step-up.
                var fingerprint = DeviceFingerprint.From(info.UserAgent);
                var trusted = await _devices.GetByFingerprintAsync(tenantId, user.Id, fingerprint, ct) is { Trusted: true };
                if (!trusted)
                    Consider(s.NewDeviceAction, "new_device");
            }
        }

        // Unverified email at sign-in.
        if (s.UnverifiedEmailAction != ConditionalAction.Allow && !user.EmailVerified)
            Consider(s.UnverifiedEmailAction, "unverified_email");

        return new ConditionalAccessDecision(action, reason);
    }
}
