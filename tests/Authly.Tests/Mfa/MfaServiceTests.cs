using System.Security.Cryptography;
using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Infrastructure.Security;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Mfa;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Authly.Tests.Mfa;

public class MfaServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private const string Base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    // --- Policy / login evaluation -----------------------------------------

    [Fact]
    public async Task Optional_policy_with_no_factors_does_not_require_mfa()
    {
        var h = new Harness();
        var decision = await h.Service.EvaluateLoginAsync(Tenant, h.NewUser());
        Assert.Equal(MfaLoginRequirement.NotRequired, decision.Requirement);
    }

    [Fact]
    public async Task User_with_an_active_factor_is_always_challenged()
    {
        var h = new Harness();
        var user = h.NewUser();
        h.Factors.Items.Add(new MfaFactor
        {
            Id = Guid.NewGuid(), UserId = user.Id, TenantId = Tenant,
            Type = MfaFactorType.Totp, Status = MfaFactorStatus.Active
        });

        var decision = await h.Service.EvaluateLoginAsync(Tenant, user);

        Assert.Equal(MfaLoginRequirement.ChallengeRequired, decision.Requirement);
        Assert.True(decision.Methods.Totp);
    }

    [Fact]
    public async Task AdminsOnly_policy_forces_enrollment_for_an_admin_without_factors()
    {
        var h = new Harness();
        await h.Service.SetPolicyAsync(Tenant, new TenantMfaSettings { Policy = MfaPolicy.AdminsOnly }, AuditContext.System);

        var admin = h.NewUser();
        admin.IsTenantAdmin = true;

        var decision = await h.Service.EvaluateLoginAsync(Tenant, admin);
        Assert.Equal(MfaLoginRequirement.EnrollmentRequired, decision.Requirement);
    }

    [Fact]
    public async Task AdminsOnly_policy_leaves_regular_users_alone()
    {
        var h = new Harness();
        await h.Service.SetPolicyAsync(Tenant, new TenantMfaSettings { Policy = MfaPolicy.AdminsOnly }, AuditContext.System);

        var decision = await h.Service.EvaluateLoginAsync(Tenant, h.NewUser());
        Assert.Equal(MfaLoginRequirement.NotRequired, decision.Requirement);
    }

    [Fact]
    public async Task Required_policy_forces_enrollment_for_everyone()
    {
        var h = new Harness();
        await h.Service.SetPolicyAsync(Tenant, new TenantMfaSettings { Policy = MfaPolicy.Required }, AuditContext.System);

        var decision = await h.Service.EvaluateLoginAsync(Tenant, h.NewUser());
        Assert.Equal(MfaLoginRequirement.EnrollmentRequired, decision.Requirement);
    }

    [Fact]
    public async Task SetPolicy_round_trips_and_preserves_other_settings_keys()
    {
        var h = new Harness();
        h.Tenants.Item.Settings = """{"branding_done":true}""";

        await h.Service.SetPolicyAsync(Tenant, new TenantMfaSettings { Policy = MfaPolicy.Required, AllowEmailOtp = false }, AuditContext.System);

        var read = await h.Service.GetPolicyAsync(Tenant);
        Assert.Equal(MfaPolicy.Required, read.Policy);
        Assert.False(read.AllowEmailOtp);
        Assert.Contains("branding_done", h.Tenants.Item.Settings); // untouched
    }

    // --- TOTP enrolment -----------------------------------------------------

    [Fact]
    public async Task Totp_enrollment_stores_encrypted_secret_and_activates_on_valid_code()
    {
        var h = new Harness();
        var user = h.NewUser();

        var enrollment = await h.Service.BeginTotpEnrollmentAsync(Tenant, user.Id, user.Email, null);
        var stored = Assert.Single(h.Factors.Items);
        Assert.Equal(MfaFactorStatus.Pending, stored.Status);
        Assert.NotEqual(enrollment.Secret, stored.Secret);                 // secret encrypted at rest
        Assert.Equal(enrollment.Secret, h.Encryption.Decrypt(stored.Secret!));

        var ok = await h.Service.ConfirmTotpEnrollmentAsync(Tenant, user.Id, enrollment.FactorId, ComputeNow(enrollment.Secret), AuditContext.System);
        Assert.True(ok);
        Assert.Equal(MfaFactorStatus.Active, h.Factors.Items.Single().Status);
    }

    [Fact]
    public async Task Totp_enrollment_rejects_a_wrong_code()
    {
        var h = new Harness();
        var user = h.NewUser();
        var enrollment = await h.Service.BeginTotpEnrollmentAsync(Tenant, user.Id, user.Email, null);

        var ok = await h.Service.ConfirmTotpEnrollmentAsync(Tenant, user.Id, enrollment.FactorId, "000000", AuditContext.System);
        // "000000" only matches by a 1-in-a-million fluke; treat the realistic case.
        if (ComputeNow(enrollment.Secret) != "000000")
            Assert.False(ok);
    }

    [Fact]
    public async Task VerifyTotp_succeeds_for_an_active_factor()
    {
        var h = new Harness();
        var user = h.NewUser();
        var enrollment = await h.Service.BeginTotpEnrollmentAsync(Tenant, user.Id, user.Email, null);
        await h.Service.ConfirmTotpEnrollmentAsync(Tenant, user.Id, enrollment.FactorId, ComputeNow(enrollment.Secret), AuditContext.System);

        Assert.True(await h.Service.VerifyTotpAsync(Tenant, user.Id, ComputeNow(enrollment.Secret), AuditContext.System));
    }

    // --- Backup codes -------------------------------------------------------

    [Fact]
    public async Task Backup_codes_are_single_use()
    {
        var h = new Harness();
        var user = h.NewUser();
        var result = await h.Service.GenerateBackupCodesAsync(Tenant, user.Id, AuditContext.System);
        Assert.Equal(10, result.Codes.Count);

        var code = result.Codes[0];
        Assert.True(await h.Service.VerifyBackupCodeAsync(Tenant, user.Id, code, AuditContext.System));
        Assert.False(await h.Service.VerifyBackupCodeAsync(Tenant, user.Id, code, AuditContext.System)); // reused
        Assert.Equal(9, await h.Service.CountUnusedBackupCodesAsync(user.Id));
    }

    [Fact]
    public async Task Backup_code_accepts_normalized_input()
    {
        var h = new Harness();
        var user = h.NewUser();
        var result = await h.Service.GenerateBackupCodesAsync(Tenant, user.Id, AuditContext.System);

        var noisy = result.Codes[0].ToUpperInvariant().Replace("-", " ");
        Assert.True(await h.Service.VerifyBackupCodeAsync(Tenant, user.Id, noisy, AuditContext.System));
    }

    [Fact]
    public async Task Regenerating_backup_codes_drops_the_old_set()
    {
        var h = new Harness();
        var user = h.NewUser();
        var first = await h.Service.GenerateBackupCodesAsync(Tenant, user.Id, AuditContext.System);
        await h.Service.GenerateBackupCodesAsync(Tenant, user.Id, AuditContext.System);

        Assert.False(await h.Service.VerifyBackupCodeAsync(Tenant, user.Id, first.Codes[0], AuditContext.System));
    }

    // --- Email OTP ----------------------------------------------------------

    [Fact]
    public async Task Email_otp_is_emailed_and_verifies_once()
    {
        var h = new Harness();
        var user = h.NewUser();

        await h.Service.SendEmailOtpAsync(Tenant, user);
        var req = Assert.Single(h.Messages.Sent);
        Assert.Equal("otp", req.TemplateKey);
        var code = req.Variables["otp"];
        Assert.Matches(@"^\d{6}$", code);

        Assert.True(await h.Service.VerifyEmailOtpAsync(Tenant, user.Id, code, AuditContext.System));
        Assert.False(await h.Service.VerifyEmailOtpAsync(Tenant, user.Id, code, AuditContext.System)); // burned
    }

    [Fact]
    public async Task Email_otp_burns_after_too_many_wrong_attempts()
    {
        var h = new Harness();
        var user = h.NewUser();
        await h.Service.SendEmailOtpAsync(Tenant, user);

        for (var i = 0; i < 5; i++)
            Assert.False(await h.Service.VerifyEmailOtpAsync(Tenant, user.Id, "999999", AuditContext.System));

        // After the attempt cap, even the right code no longer works.
        var code = h.Messages.Sent[0].Variables["otp"];
        Assert.False(await h.Service.VerifyEmailOtpAsync(Tenant, user.Id, code, AuditContext.System));
    }

    [Fact]
    public async Task Disable_factor_revokes_it()
    {
        var h = new Harness();
        var user = h.NewUser();
        var factor = new MfaFactor
        {
            Id = Guid.NewGuid(), UserId = user.Id, TenantId = Tenant,
            Type = MfaFactorType.Totp, Status = MfaFactorStatus.Active
        };
        h.Factors.Items.Add(factor);

        await h.Service.DisableFactorAsync(Tenant, user.Id, factor.Id, AuditContext.System);
        Assert.Equal(MfaFactorStatus.Revoked, factor.Status);
    }

    // --- reference TOTP for tests ------------------------------------------

    private static string ComputeNow(string base32Secret)
    {
        var key = Base32Decode(base32Secret);
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / 30;
        Span<byte> ctr = stackalloc byte[8];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(ctr, counter);
        var hash = HMACSHA1.HashData(key, ctr.ToArray());
        var offset = hash[^1] & 0x0F;
        var bin = ((hash[offset] & 0x7F) << 24) | ((hash[offset + 1] & 0xFF) << 16)
                  | ((hash[offset + 2] & 0xFF) << 8) | (hash[offset + 3] & 0xFF);
        return (bin % 1_000_000).ToString().PadLeft(6, '0');
    }

    private static byte[] Base32Decode(string input)
    {
        input = input.TrimEnd('=').ToUpperInvariant();
        var output = new List<byte>();
        int buffer = 0, bits = 0;
        foreach (var c in input)
        {
            buffer = (buffer << 5) | Base32Alphabet.IndexOf(c);
            bits += 5;
            if (bits >= 8) { bits -= 8; output.Add((byte)((buffer >> bits) & 0xFF)); }
        }
        return output.ToArray();
    }

    // --- harness ------------------------------------------------------------

    private sealed class Harness
    {
        public readonly FakeFactorRepo Factors = new();
        public readonly FakeBackupRepo Backup = new();
        public readonly FakeOtpRepo Otp = new();
        public readonly FakeUserRoleRepo Roles = new();
        public readonly FakeTenantRepo Tenants = new();
        public readonly FakeMessageQueue Messages = new();
        public readonly AesEncryptionService Encryption =
            new(Options.Create(new EncryptionOptions { Key = "3J8mZ1qg9X0vQpYb2sR7tU4wK6nL5cD8eF1aH0iJ2kM=" }));
        public readonly MfaService Service;

        public Harness()
        {
            Service = new MfaService(Factors, Backup, Otp, Roles, Tenants, new TotpService(),
                Encryption, new Sha256TokenHasher(), new CredentialGenerator(), Messages,
                new RecordingAuditLogger(), NullLogger<MfaService>.Instance);
        }

        public User NewUser() => new()
        {
            Id = Guid.NewGuid(), TenantId = Tenant, Email = "ada@example.com", FirstName = "Ada"
        };
    }

    private sealed class FakeFactorRepo : IMfaFactorRepository
    {
        public readonly List<MfaFactor> Items = new();
        public Task<MfaFactor?> GetByIdAsync(Guid t, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(f => f.TenantId == t && f.Id == id));
        public Task<IReadOnlyList<MfaFactor>> ListByUserAsync(Guid t, Guid u, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MfaFactor>>(Items.Where(f => f.TenantId == t && f.UserId == u).ToList());
        public Task<IReadOnlyList<MfaFactor>> ListActiveByUserAsync(Guid t, Guid u, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<MfaFactor>>(Items.Where(f => f.TenantId == t && f.UserId == u && f.Status == MfaFactorStatus.Active).ToList());
        public Task<MfaFactor?> GetActiveByTypeAsync(Guid t, Guid u, MfaFactorType type, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(f => f.TenantId == t && f.UserId == u && f.Type == type && f.Status == MfaFactorStatus.Active));
        public Task<bool> AnyActiveAsync(Guid t, Guid u, CancellationToken ct = default)
            => Task.FromResult(Items.Any(f => f.TenantId == t && f.UserId == u && f.Status == MfaFactorStatus.Active));
        public Task AddAsync(MfaFactor f, CancellationToken ct = default)
        {
            if (f.Id == Guid.Empty) f.Id = Guid.NewGuid();
            Items.Add(f);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(MfaFactor f, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeBackupRepo : IMfaBackupCodeRepository
    {
        public readonly List<MfaBackupCode> Items = new();
        public Task AddRangeAsync(IEnumerable<MfaBackupCode> codes, CancellationToken ct = default)
        {
            foreach (var c in codes) { if (c.Id == Guid.Empty) c.Id = Guid.NewGuid(); Items.Add(c); }
            return Task.CompletedTask;
        }
        public Task<MfaBackupCode?> GetUnusedByHashAsync(Guid u, string hash, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(c => c.UserId == u && c.CodeHash == hash && !c.Used));
        public Task<int> CountUnusedAsync(Guid u, CancellationToken ct = default)
            => Task.FromResult(Items.Count(c => c.UserId == u && !c.Used));
        public Task DeleteAllForUserAsync(Guid u, CancellationToken ct = default)
        {
            Items.RemoveAll(c => c.UserId == u);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(MfaBackupCode c, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeOtpRepo : IOtpCodeRepository
    {
        public readonly List<OtpCode> Items = new();
        public Task AddAsync(OtpCode o, CancellationToken ct = default)
        {
            if (o.Id == Guid.Empty) o.Id = Guid.NewGuid();
            Items.Add(o);
            return Task.CompletedTask;
        }
        public Task<OtpCode?> GetLatestActiveAsync(Guid t, Guid u, OtpChannel ch, CancellationToken ct = default)
            => Task.FromResult(Items.Where(o => o.TenantId == t && o.UserId == u && o.Channel == ch && !o.Used && o.ExpiresAt > DateTimeOffset.UtcNow)
                .OrderByDescending(o => o.CreatedAt).FirstOrDefault());
        public Task InvalidateOutstandingAsync(Guid t, Guid u, OtpChannel ch, CancellationToken ct = default)
        {
            foreach (var o in Items.Where(o => o.TenantId == t && o.UserId == u && o.Channel == ch && !o.Used)) o.Used = true;
            return Task.CompletedTask;
        }
        public Task UpdateAsync(OtpCode o, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUserRoleRepo : IUserRoleRepository
    {
        public Task AssignAsync(UserRole a, CancellationToken ct = default) => Task.CompletedTask;
        public Task RemoveAsync(Guid t, Guid u, Guid r, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<Role>> ListRolesForUserAsync(Guid t, Guid u, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Role>>(Array.Empty<Role>());
        public Task<IReadOnlyList<Guid>> ListUserIdsForRoleAsync(Guid t, Guid r, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Guid>>(Array.Empty<Guid>());
        public Task<IReadOnlyList<string>> GetRoleNamesAsync(Guid t, Guid u, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
        public Task<IReadOnlyList<string>> GetPermissionNamesAsync(Guid t, Guid u, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>(Array.Empty<string>());
    }

    private sealed class FakeTenantRepo : ITenantRepository
    {
        public readonly Tenant Item = new() { Id = Tenant, Slug = "acme", Name = "Acme", Settings = "{}" };
        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<Tenant?>(id == Item.Id ? Item : null);
        public Task<Tenant?> GetBySlugAsync(string s, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetByCustomDomainOrNullAsync(string h, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>(new[] { Item });
        public Task<bool> SlugExistsAsync(string s, CancellationToken ct = default) => Task.FromResult(false);
        public Task AddAsync(Tenant t, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateAsync(Tenant t, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeMessageQueue : IMessageQueue
    {
        public readonly List<MessageSendRequest> Sent = new();
        public void Enqueue(MessageSendRequest request) => Sent.Add(request);
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null,
            string? resourceType = null, Guid? resourceId = null, string result = "success",
            object? metadata = null, CancellationToken ct = default) => Task.CompletedTask;
    }
}
