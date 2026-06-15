using Authly.Core.Enums;

namespace Authly.Modules.AdvancedAuth;

/// <summary>Thrown when an advanced-auth operation is given invalid input.</summary>
public sealed class AdvancedAuthException : Exception
{
    public AdvancedAuthException(string message) : base(message) { }
}

/// <summary>Outcome of starting an email/phone change.</summary>
public enum ContactChangeOutcome
{
    Started,

    /// <summary>The new value is already in use (email uniqueness within the tenant).</summary>
    AlreadyInUse,

    /// <summary>A change is already pending and the cooldown hasn't elapsed.</summary>
    Cooldown
}

/// <summary>A passkey shown in the portal list.</summary>
public sealed record PasskeySummary(Guid Id, string? FriendlyName, DateTimeOffset CreatedAt, DateTimeOffset? LastUsedAt);

/// <summary>A recovery contact shown in the portal list.</summary>
public sealed record RecoveryContactSummary(Guid Id, ContactType Type, string Value, bool Verified, DateTimeOffset CreatedAt);
