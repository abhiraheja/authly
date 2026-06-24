using System.Text.Json;
using System.Text.Json.Nodes;
using Authly.Core.Authorization;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Messaging;
using Microsoft.Extensions.Logging;

namespace Authly.Modules.Mfa;

/// <inheritdoc />
public sealed class MfaService : IMfaService
{
    private const string Issuer = "Authly";
    private const int BackupCodeCount = 10;
    private const int OtpDigits = 6;
    private const int OtpMaxAttempts = 5;
    private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(10);

    private readonly IMfaFactorRepository _factors;
    private readonly IMfaBackupCodeRepository _backupCodes;
    private readonly IOtpCodeRepository _otpCodes;
    private readonly IUserRoleRepository _userRoles;
    private readonly ITenantRepository _tenants;
    private readonly ITotpService _totp;
    private readonly IEncryptionService _encryption;
    private readonly ITokenHasher _hasher;
    private readonly ICredentialGenerator _generator;
    private readonly IMessageQueue _messages;
    private readonly IAuditLogger _audit;
    private readonly ILogger<MfaService> _logger;

    public MfaService(
        IMfaFactorRepository factors,
        IMfaBackupCodeRepository backupCodes,
        IOtpCodeRepository otpCodes,
        IUserRoleRepository userRoles,
        ITenantRepository tenants,
        ITotpService totp,
        IEncryptionService encryption,
        ITokenHasher hasher,
        ICredentialGenerator generator,
        IMessageQueue messages,
        IAuditLogger audit,
        ILogger<MfaService> logger)
    {
        _factors = factors;
        _backupCodes = backupCodes;
        _otpCodes = otpCodes;
        _userRoles = userRoles;
        _tenants = tenants;
        _totp = totp;
        _encryption = encryption;
        _hasher = hasher;
        _generator = generator;
        _messages = messages;
        _audit = audit;
        _logger = logger;
    }

    // --- Policy -------------------------------------------------------------

    public async Task<TenantMfaSettings> GetPolicyAsync(Guid tenantId, CancellationToken ct = default)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct);
        return ReadSettings(tenant?.Settings);
    }

    public async Task SetPolicyAsync(Guid tenantId, TenantMfaSettings settings, AuditContext actor, CancellationToken ct = default)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, ct)
            ?? throw new InvalidOperationException($"Tenant '{tenantId}' not found.");

        // Merge the mfa node into the existing settings JSON without clobbering other keys.
        var root = ParseObject(tenant.Settings);
        root["mfa"] = JsonSerializer.SerializeToNode(settings);
        tenant.Settings = root.ToJsonString();
        tenant.UpdatedAt = DateTimeOffset.UtcNow;
        await _tenants.UpdateAsync(tenant, ct);

        await _audit.LogAsync("mfa.policy_changed", actor, tenantId, "tenant", tenantId,
            metadata: new { settings.Policy }, ct: ct);
    }

    public async Task<MfaLoginDecision> EvaluateLoginAsync(Guid tenantId, User user, CancellationToken ct = default)
    {
        var policy = await GetPolicyAsync(tenantId, ct);
        var hasActive = await _factors.AnyActiveAsync(tenantId, user.Id, ct);

        // A user who has opted in is always challenged, regardless of policy.
        if (hasActive)
            return new MfaLoginDecision(MfaLoginRequirement.ChallengeRequired,
                await GetAvailableMethodsAsync(tenantId, user.Id, ct));

        var required = policy.Policy switch
        {
            MfaPolicy.Required => true,
            MfaPolicy.AdminsOnly => await IsAdminAsync(tenantId, user, ct),
            _ => false
        };

        return required
            ? new MfaLoginDecision(MfaLoginRequirement.EnrollmentRequired, new MfaAvailableMethods(false, false, false))
            : new MfaLoginDecision(MfaLoginRequirement.NotRequired, new MfaAvailableMethods(false, false, false));
    }

    public async Task<MfaAvailableMethods> GetAvailableMethodsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
    {
        var active = await _factors.ListActiveByUserAsync(tenantId, userId, ct);
        var hasBackup = await _backupCodes.CountUnusedAsync(userId, ct) > 0;
        return new MfaAvailableMethods(
            Totp: active.Any(f => f.Type == MfaFactorType.Totp),
            EmailOtp: active.Any(f => f.Type == MfaFactorType.EmailOtp),
            BackupCodes: hasBackup);
    }

    // --- Factor management --------------------------------------------------

    public Task<IReadOnlyList<MfaFactor>> ListFactorsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => _factors.ListByUserAsync(tenantId, userId, ct);

    public async Task DisableFactorAsync(Guid tenantId, Guid userId, Guid factorId, AuditContext actor, CancellationToken ct = default)
    {
        var factor = await _factors.GetByIdAsync(tenantId, factorId, ct);
        if (factor is null || factor.UserId != userId)
            throw new MfaFactorNotFoundException(factorId);

        factor.Status = MfaFactorStatus.Revoked;
        await _factors.UpdateAsync(factor, ct);

        await _audit.LogAsync("mfa.factor_disabled", actor, tenantId, "mfa_factor", factor.Id,
            metadata: new { type = factor.Type.ToString() }, ct: ct);
    }

    // --- TOTP enrolment -----------------------------------------------------

    public async Task<TotpEnrollment> BeginTotpEnrollmentAsync(Guid tenantId, Guid userId, string accountName, string? friendlyName, CancellationToken ct = default)
    {
        var policy = await GetPolicyAsync(tenantId, ct);
        if (!policy.AllowTotp)
            throw new MfaMethodNotAllowedException("totp");

        var secret = _totp.GenerateSecret();
        var factor = new MfaFactor
        {
            UserId = userId,
            TenantId = tenantId,
            Type = MfaFactorType.Totp,
            Secret = _encryption.Encrypt(secret),     // encrypted at rest
            Status = MfaFactorStatus.Pending,
            FriendlyName = string.IsNullOrWhiteSpace(friendlyName) ? "Authenticator app" : friendlyName!.Trim(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        await _factors.AddAsync(factor, ct);

        var uri = _totp.BuildProvisioningUri(secret, accountName, Issuer);
        return new TotpEnrollment(factor.Id, secret, uri);
    }

    public async Task<bool> ConfirmTotpEnrollmentAsync(Guid tenantId, Guid userId, Guid factorId, string code, AuditContext actor, CancellationToken ct = default)
    {
        var factor = await _factors.GetByIdAsync(tenantId, factorId, ct);
        if (factor is null || factor.UserId != userId || factor.Type != MfaFactorType.Totp || factor.Secret is null)
            throw new MfaFactorNotFoundException(factorId);

        if (factor.Status == MfaFactorStatus.Revoked)
            throw new MfaFactorNotFoundException(factorId);

        var secret = _encryption.Decrypt(factor.Secret);
        if (!_totp.Verify(secret, code))
            return false;

        factor.Status = MfaFactorStatus.Active;
        factor.LastUsedAt = DateTimeOffset.UtcNow;
        await _factors.UpdateAsync(factor, ct);

        await _audit.LogAsync("mfa.factor_enrolled", actor, tenantId, "mfa_factor", factor.Id,
            metadata: new { type = "totp" }, ct: ct);
        return true;
    }

    // --- Email OTP ----------------------------------------------------------

    public async Task EnableEmailOtpAsync(Guid tenantId, User user, AuditContext actor, CancellationToken ct = default)
    {
        var policy = await GetPolicyAsync(tenantId, ct);
        if (!policy.AllowEmailOtp)
            throw new MfaMethodNotAllowedException("email_otp");

        var existing = await _factors.GetActiveByTypeAsync(tenantId, user.Id, MfaFactorType.EmailOtp, ct);
        if (existing is not null)
            return; // already enabled

        await _factors.AddAsync(new MfaFactor
        {
            UserId = user.Id,
            TenantId = tenantId,
            Type = MfaFactorType.EmailOtp,
            Status = MfaFactorStatus.Active,
            FriendlyName = $"Email to {user.Email}",
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        await _audit.LogAsync("mfa.factor_enrolled", actor, tenantId, "user", user.Id,
            metadata: new { type = "email_otp" }, ct: ct);
    }

    public Task SendEmailOtpAsync(Guid tenantId, User user, CancellationToken ct = default)
        => SendOtpAsync(tenantId, user, OtpChannel.Email, MessageChannel.Email, user.Email, ct);

    public Task SendPhoneOtpAsync(Guid tenantId, User user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Phone))
            throw new InvalidOperationException("Cannot send a phone OTP to a user without a phone number.");
        return SendOtpAsync(tenantId, user, OtpChannel.WhatsApp, MessageChannel.WhatsApp, user.Phone!, ct);
    }

    /// <summary>Generates and delivers a fresh OTP over the given channel (invalidating any
    /// outstanding one for that channel). The raw code travels only inside the queued message
    /// variables; it is never logged.</summary>
    private async Task SendOtpAsync(Guid tenantId, User user, OtpChannel otpChannel,
        MessageChannel messageChannel, string recipient, CancellationToken ct)
    {
        await _otpCodes.InvalidateOutstandingAsync(tenantId, user.Id, otpChannel, ct);

        var raw = _generator.GenerateNumericOtp(OtpDigits);
        await _otpCodes.AddAsync(new OtpCode
        {
            UserId = user.Id,
            TenantId = tenantId,
            Channel = otpChannel,
            CodeHash = _hasher.Hash(raw),
            Attempts = 0,
            ExpiresAt = DateTimeOffset.UtcNow.Add(OtpLifetime),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        _messages.Enqueue(new MessageSendRequest(tenantId, MessageTemplateKeys.Otp,
            messageChannel, recipient, new Dictionary<string, string>
            {
                ["user_name"] = string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName!,
                ["otp"] = raw,
                ["expiry_minutes"] = "10"
            }));
        _logger.LogInformation("{Channel} OTP issued for user {UserId} in tenant {TenantId}.", otpChannel, user.Id, tenantId);
    }

    // --- Backup codes -------------------------------------------------------

    public async Task<BackupCodesResult> GenerateBackupCodesAsync(Guid tenantId, Guid userId, AuditContext actor, CancellationToken ct = default)
    {
        await _backupCodes.DeleteAllForUserAsync(userId, ct);

        var now = DateTimeOffset.UtcNow;
        var raw = new List<string>(BackupCodeCount);
        var entities = new List<MfaBackupCode>(BackupCodeCount);
        for (var i = 0; i < BackupCodeCount; i++)
        {
            var code = _generator.GenerateBackupCode();
            raw.Add(code);
            entities.Add(new MfaBackupCode
            {
                UserId = userId,
                CodeHash = _hasher.Hash(NormalizeBackupCode(code)),
                Used = false,
                CreatedAt = now
            });
        }
        await _backupCodes.AddRangeAsync(entities, ct);

        await _audit.LogAsync("mfa.backup_codes_generated", actor, tenantId, "user", userId,
            metadata: new { count = BackupCodeCount }, ct: ct);
        return new BackupCodesResult(raw);
    }

    public Task<int> CountUnusedBackupCodesAsync(Guid userId, CancellationToken ct = default)
        => _backupCodes.CountUnusedAsync(userId, ct);

    // --- Login challenge verification --------------------------------------

    public async Task<bool> VerifyTotpAsync(Guid tenantId, Guid userId, string code, AuditContext actor, CancellationToken ct = default)
    {
        var factor = await _factors.GetActiveByTypeAsync(tenantId, userId, MfaFactorType.Totp, ct);
        if (factor?.Secret is null)
            return false;

        var ok = _totp.Verify(_encryption.Decrypt(factor.Secret), code);
        if (ok)
        {
            factor.LastUsedAt = DateTimeOffset.UtcNow;
            await _factors.UpdateAsync(factor, ct);
        }
        await LogVerifyAsync(tenantId, userId, "totp", ok, actor, ct);
        return ok;
    }

    public Task<bool> VerifyEmailOtpAsync(Guid tenantId, Guid userId, string code, AuditContext actor, CancellationToken ct = default)
        => VerifyOtpAsync(tenantId, userId, code, OtpChannel.Email, "email_otp", actor, ct);

    public Task<bool> VerifyPhoneOtpAsync(Guid tenantId, Guid userId, string code, AuditContext actor, CancellationToken ct = default)
        => VerifyOtpAsync(tenantId, userId, code, OtpChannel.WhatsApp, "phone_otp", actor, ct);

    private async Task<bool> VerifyOtpAsync(Guid tenantId, Guid userId, string code, OtpChannel channel,
        string auditMethod, AuditContext actor, CancellationToken ct)
    {
        var otp = await _otpCodes.GetLatestActiveAsync(tenantId, userId, channel, ct);
        if (otp is null)
        {
            await LogVerifyAsync(tenantId, userId, auditMethod, false, actor, ct);
            return false;
        }

        if (otp.Attempts >= OtpMaxAttempts)
        {
            otp.Used = true; // burn it — too many guesses
            await _otpCodes.UpdateAsync(otp, ct);
            await LogVerifyAsync(tenantId, userId, auditMethod, false, actor, ct);
            return false;
        }

        var ok = FixedTimeEquals(otp.CodeHash, _hasher.Hash((code ?? string.Empty).Trim()));
        if (ok)
        {
            otp.Used = true;
            await _otpCodes.UpdateAsync(otp, ct);
        }
        else
        {
            otp.Attempts++;
            await _otpCodes.UpdateAsync(otp, ct);
        }

        await LogVerifyAsync(tenantId, userId, auditMethod, ok, actor, ct);
        return ok;
    }

    public async Task<bool> VerifyBackupCodeAsync(Guid tenantId, Guid userId, string rawCode, AuditContext actor, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
            return false;

        var match = await _backupCodes.GetUnusedByHashAsync(userId, _hasher.Hash(NormalizeBackupCode(rawCode)), ct);
        if (match is null)
        {
            await LogVerifyAsync(tenantId, userId, "backup_code", false, actor, ct);
            return false;
        }

        match.Used = true;
        await _backupCodes.UpdateAsync(match, ct);
        await LogVerifyAsync(tenantId, userId, "backup_code", true, actor, ct);
        return true;
    }

    // --- helpers ------------------------------------------------------------

    private async Task<bool> IsAdminAsync(Guid tenantId, User user, CancellationToken ct)
    {
        var roles = await _userRoles.GetRoleNamesAsync(tenantId, user.Id, ct);
        return roles.Contains(SystemRbac.TenantAdmin) || roles.Contains(SystemRbac.SuperAdmin);
    }

    private Task LogVerifyAsync(Guid tenantId, Guid userId, string method, bool ok, AuditContext actor, CancellationToken ct)
        => _audit.LogAsync(ok ? "mfa.challenge_succeeded" : "mfa.challenge_failed", actor, tenantId,
            "user", userId, result: ok ? "success" : "failure", metadata: new { method }, ct: ct);

    private static TenantMfaSettings ReadSettings(string? settingsJson)
    {
        if (string.IsNullOrWhiteSpace(settingsJson))
            return new TenantMfaSettings();
        try
        {
            var root = ParseObject(settingsJson);
            if (root["mfa"] is { } mfa)
                return mfa.Deserialize<TenantMfaSettings>() ?? new TenantMfaSettings();
        }
        catch (JsonException)
        {
            // Malformed settings shouldn't take down login — fall back to safe defaults.
        }
        return new TenantMfaSettings();
    }

    private static JsonObject ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new JsonObject();
        return JsonNode.Parse(json) as JsonObject ?? new JsonObject();
    }

    private static string NormalizeBackupCode(string code)
        => new string(code.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static bool FixedTimeEquals(string a, string b)
        => System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
            System.Text.Encoding.UTF8.GetBytes(a), System.Text.Encoding.UTF8.GetBytes(b));
}
