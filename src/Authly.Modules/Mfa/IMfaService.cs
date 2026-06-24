using Authly.Core.Entities;
using Authly.Modules.Common;

namespace Authly.Modules.Mfa;

/// <summary>
/// Multi-factor authentication: enrolment (TOTP / email OTP), backup codes, login-time
/// challenge verification, and per-tenant policy. Tenant-scoped; the caller is responsible for
/// having bound the tenant context (RLS backstop applies to every read/write here).
/// </summary>
public interface IMfaService
{
    // --- Policy ---
    Task<TenantMfaSettings> GetPolicyAsync(Guid tenantId, CancellationToken ct = default);
    Task SetPolicyAsync(Guid tenantId, TenantMfaSettings settings, AuditContext actor, CancellationToken ct = default);

    /// <summary>Decides whether the just-authenticated user must pass / enrol MFA before sign-in.</summary>
    Task<MfaLoginDecision> EvaluateLoginAsync(Guid tenantId, User user, CancellationToken ct = default);

    /// <summary>The challenge methods currently available to a user (for re-rendering the gate).</summary>
    Task<MfaAvailableMethods> GetAvailableMethodsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);

    // --- Factor management ---
    Task<IReadOnlyList<MfaFactor>> ListFactorsAsync(Guid tenantId, Guid userId, CancellationToken ct = default);
    Task DisableFactorAsync(Guid tenantId, Guid userId, Guid factorId, AuditContext actor, CancellationToken ct = default);

    // --- TOTP enrolment ---
    Task<TotpEnrollment> BeginTotpEnrollmentAsync(Guid tenantId, Guid userId, string accountName, string? friendlyName, CancellationToken ct = default);
    Task<bool> ConfirmTotpEnrollmentAsync(Guid tenantId, Guid userId, Guid factorId, string code, AuditContext actor, CancellationToken ct = default);

    // --- Email OTP ---
    /// <summary>Enables email OTP as an active factor for the user (no enrolment code required).</summary>
    Task EnableEmailOtpAsync(Guid tenantId, User user, AuditContext actor, CancellationToken ct = default);

    /// <summary>Generates and emails a fresh OTP (invalidating any outstanding one).</summary>
    Task SendEmailOtpAsync(Guid tenantId, User user, CancellationToken ct = default);

    /// <summary>Generates and sends a fresh OTP to the user's phone over WhatsApp (invalidating any
    /// outstanding one). Throws if the user has no phone number.</summary>
    Task SendPhoneOtpAsync(Guid tenantId, User user, CancellationToken ct = default);

    // --- Backup codes ---
    Task<BackupCodesResult> GenerateBackupCodesAsync(Guid tenantId, Guid userId, AuditContext actor, CancellationToken ct = default);
    Task<int> CountUnusedBackupCodesAsync(Guid userId, CancellationToken ct = default);

    // --- Login challenge verification ---
    Task<bool> VerifyTotpAsync(Guid tenantId, Guid userId, string code, AuditContext actor, CancellationToken ct = default);
    Task<bool> VerifyEmailOtpAsync(Guid tenantId, Guid userId, string code, AuditContext actor, CancellationToken ct = default);
    Task<bool> VerifyPhoneOtpAsync(Guid tenantId, Guid userId, string code, AuditContext actor, CancellationToken ct = default);
    Task<bool> VerifyBackupCodeAsync(Guid tenantId, Guid userId, string rawCode, AuditContext actor, CancellationToken ct = default);
}
