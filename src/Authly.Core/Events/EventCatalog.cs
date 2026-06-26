namespace Authly.Core.Events;

/// <summary>
/// The canonical catalogue of domain events that can route to webhooks (§4.12). Event names are
/// stable identifiers (<c>resource.action</c>); tenants subscribe webhook endpoints to any subset
/// (or the wildcard <c>*</c>). These mirror the audit-log event names so every audited state change
/// can fan out as a webhook.
/// </summary>
public static class EventCatalog
{
    // --- Authentication ---
    public const string UserRegistered = "user.registered";
    public const string UserLogin = "user.login";
    public const string UserLoginFailed = "user.login_failed";
    public const string UserLogout = "user.logout";
    public const string UserEmailVerified = "user.email_verified";
    public const string UserPasswordResetRequested = "user.password_reset_requested";
    public const string UserPasswordReset = "user.password_reset";
    public const string UserPasswordChanged = "user.password_changed";

    // --- User lifecycle ---
    public const string UserCreated = "user.created";
    public const string UserUpdated = "user.updated";
    /// <summary>A user edited their own profile via self-service (first/last name, timezone, locale).</summary>
    public const string UserProfileUpdated = "user.profile_updated";
    public const string UserSuspended = "user.suspended";
    public const string UserReactivated = "user.reactivated";
    public const string UserDeleted = "user.deleted";
    public const string UserForcePasswordReset = "user.force_password_reset";
    public const string UserRoleAssigned = "user.role_assigned";
    public const string UserRoleRemoved = "user.role_removed";

    // --- MFA ---
    public const string MfaEnrolled = "mfa.enrolled";
    public const string MfaEnabled = "mfa.enabled";
    public const string MfaDisabled = "mfa.disabled";
    public const string MfaChallengeSucceeded = "mfa.challenge_succeeded";
    public const string MfaChallengeFailed = "mfa.challenge_failed";
    public const string MfaBackupCodesGenerated = "mfa.backup_codes_generated";
    public const string MfaBackupCodeUsed = "mfa.backup_code_used";
    public const string MfaPolicyUpdated = "mfa.policy_updated";

    // --- Sessions ---
    public const string SessionCreated = "session.created";
    public const string SessionRevoked = "session.revoked";
    public const string SessionRefreshed = "session.refreshed";
    public const string SessionExpired = "session.expired";

    // --- Organization / tenant ---
    public const string TenantCreated = "tenant.created";
    public const string TenantUpdated = "tenant.updated";
    public const string TenantSuspended = "tenant.suspended";
    public const string RoleCreated = "role.created";
    public const string RoleUpdated = "role.updated";
    public const string RoleDeleted = "role.deleted";
    public const string PermissionCreated = "permission.created";

    // --- Tokens ---
    public const string TokenIssued = "token.issued";
    public const string TokenRefreshed = "token.refreshed";
    public const string TokenRevoked = "token.revoked";

    // --- Applications & API keys ---
    public const string ApplicationCreated = "application.created";
    public const string ApplicationUpdated = "application.updated";
    public const string ApplicationDeleted = "application.deleted";
    public const string ApplicationSecretRotated = "application.secret_rotated";
    public const string ApiKeyCreated = "api_key.created";
    public const string ApiKeyRevoked = "api_key.revoked";

    // --- Social identity ---
    public const string SocialLinked = "social.linked";
    public const string SocialUnlinked = "social.unlinked";
    public const string SocialLogin = "social.login";

    /// <summary>Wildcard subscription — an endpoint listing this receives every event.</summary>
    public const string Wildcard = "*";

    /// <summary>Every catalogued event name, for the subscription UI.</summary>
    public static readonly IReadOnlyList<string> All = new[]
    {
        UserRegistered, UserLogin, UserLoginFailed, UserLogout, UserEmailVerified,
        UserPasswordResetRequested, UserPasswordReset, UserPasswordChanged,
        UserCreated, UserUpdated, UserProfileUpdated, UserSuspended, UserReactivated, UserDeleted,
        UserForcePasswordReset, UserRoleAssigned, UserRoleRemoved,
        MfaEnrolled, MfaEnabled, MfaDisabled, MfaChallengeSucceeded, MfaChallengeFailed,
        MfaBackupCodesGenerated, MfaBackupCodeUsed, MfaPolicyUpdated,
        SessionCreated, SessionRevoked, SessionRefreshed, SessionExpired,
        TenantCreated, TenantUpdated, TenantSuspended, RoleCreated, RoleUpdated, RoleDeleted, PermissionCreated,
        TokenIssued, TokenRefreshed, TokenRevoked,
        ApplicationCreated, ApplicationUpdated, ApplicationDeleted, ApplicationSecretRotated,
        ApiKeyCreated, ApiKeyRevoked,
        SocialLinked, SocialUnlinked, SocialLogin
    };

    /// <summary>True when <paramref name="name"/> is a known catalogue event (the wildcard is not).</summary>
    public static bool IsKnown(string name) => All.Contains(name);
}
