using Authly.Core.Entities;
using Authly.Core.Interfaces;
using Authly.Core.Security;
using Authly.Infrastructure.Security;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Security;
using Microsoft.Extensions.Options;

namespace Authly.Tests.Security;

public class LockoutAndSettingsTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    // --- AccountLockoutService ---------------------------------------------

    [Fact]
    public async Task Lockout_locks_after_the_threshold_and_resets_on_success()
    {
        var store = new FakeAttemptStore();
        var settings = new FakeSettings(new TenantSecuritySettings { LockoutEnabled = true, LockoutThreshold = 3 });
        var svc = new AccountLockoutService(store, settings);

        Assert.False(await svc.IsLockedAsync(Tenant, "a@x.com"));
        await svc.RecordFailureAsync(Tenant, "a@x.com");
        await svc.RecordFailureAsync(Tenant, "a@x.com");
        Assert.False(await svc.IsLockedAsync(Tenant, "a@x.com")); // below threshold
        await svc.RecordFailureAsync(Tenant, "a@x.com");          // hits threshold → locked
        Assert.True(await svc.IsLockedAsync(Tenant, "a@x.com"));

        await svc.ResetAsync(Tenant, "a@x.com");
        Assert.False(await svc.IsLockedAsync(Tenant, "a@x.com"));
    }

    [Fact]
    public async Task Lockout_is_a_no_op_when_disabled()
    {
        var store = new FakeAttemptStore();
        var settings = new FakeSettings(new TenantSecuritySettings { LockoutEnabled = false, LockoutThreshold = 1 });
        var svc = new AccountLockoutService(store, settings);

        await svc.RecordFailureAsync(Tenant, "a@x.com");
        Assert.False(await svc.IsLockedAsync(Tenant, "a@x.com"));
    }

    // --- SecuritySettingsService -------------------------------------------

    [Fact]
    public async Task Settings_round_trip_and_encrypt_the_captcha_secret_write_only()
    {
        var repo = new FakeTenantRepo();
        var tenant = new Tenant { Id = Tenant, Name = "Acme", Slug = "acme", Settings = "{}" };
        repo.Store[Tenant] = tenant;
        var enc = new AesEncryptionService(Options.Create(new EncryptionOptions { Key = "3J8mZ1qg9X0vQpYb2sR7tU4wK6nL5cD8eF1aH0iJ2kM=" }));
        var svc = new SecuritySettingsService(repo, enc, new NullAudit());

        await svc.SaveAsync(Tenant, new TenantSecuritySettings
        {
            CaptchaEnabled = true, CaptchaProvider = "hcaptcha", CaptchaSiteKey = "site",
            BlockedEmailDomains = new() { "spam.example" }
        }, newCaptchaSecret: "super-secret", AuditContext.System);

        var loaded = await svc.GetAsync(Tenant);
        Assert.True(loaded.CaptchaEnabled);
        Assert.Equal("hcaptcha", loaded.CaptchaProvider);
        Assert.Contains("spam.example", loaded.BlockedEmailDomains);
        Assert.NotEqual("super-secret", loaded.CaptchaSecretEncrypted);          // stored encrypted
        Assert.Equal("super-secret", svc.DecryptCaptchaSecret(loaded));          // decrypts back

        // Saving again with a blank secret preserves the existing encrypted value.
        await svc.SaveAsync(Tenant, new TenantSecuritySettings { CaptchaEnabled = true, CaptchaProvider = "hcaptcha" },
            newCaptchaSecret: null, AuditContext.System);
        Assert.Equal("super-secret", svc.DecryptCaptchaSecret(await svc.GetAsync(Tenant)));
    }

    [Fact]
    public async Task Settings_preserve_other_nodes_in_the_json()
    {
        var repo = new FakeTenantRepo();
        repo.Store[Tenant] = new Tenant { Id = Tenant, Name = "Acme", Slug = "acme", Settings = """{"mfa":{"Policy":"Required"}}""" };
        var enc = new AesEncryptionService(Options.Create(new EncryptionOptions { Key = "3J8mZ1qg9X0vQpYb2sR7tU4wK6nL5cD8eF1aH0iJ2kM=" }));
        var svc = new SecuritySettingsService(repo, enc, new NullAudit());

        await svc.SaveAsync(Tenant, new TenantSecuritySettings { LockoutThreshold = 7 }, null, AuditContext.System);

        Assert.Contains("\"mfa\"", repo.Store[Tenant].Settings);   // mfa node untouched
        Assert.Equal(7, (await svc.GetAsync(Tenant)).LockoutThreshold);
    }

    // --- fakes -------------------------------------------------------------

    private sealed class FakeAttemptStore : ILoginAttemptStore
    {
        private readonly Dictionary<string, int> _fails = new();
        private readonly Dictionary<string, DateTimeOffset> _locks = new();
        public Task<LoginAttemptState> GetAsync(string key, CancellationToken ct = default)
            => Task.FromResult(new LoginAttemptState(_fails.GetValueOrDefault(key),
                _locks.TryGetValue(key, out var u) ? u : null));
        public Task<int> RecordFailureAsync(string key, TimeSpan retention, CancellationToken ct = default)
            => Task.FromResult(_fails[key] = _fails.GetValueOrDefault(key) + 1);
        public Task LockAsync(string key, DateTimeOffset until, CancellationToken ct = default) { _locks[key] = until; return Task.CompletedTask; }
        public Task ResetAsync(string key, CancellationToken ct = default) { _fails.Remove(key); _locks.Remove(key); return Task.CompletedTask; }
    }

    private sealed class FakeSettings : ISecuritySettingsService
    {
        private readonly TenantSecuritySettings _s;
        public FakeSettings(TenantSecuritySettings s) => _s = s;
        public Task<TenantSecuritySettings> GetAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult(_s);
        public Task SaveAsync(Guid tenantId, TenantSecuritySettings settings, string? newCaptchaSecret, AuditContext actor, CancellationToken ct = default) => Task.CompletedTask;
        public string? DecryptCaptchaSecret(TenantSecuritySettings settings) => null;
    }

    private sealed class FakeTenantRepo : ITenantRepository
    {
        public readonly Dictionary<Guid, Tenant> Store = new();
        public Task<Tenant?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(Store.GetValueOrDefault(id));
        public Task<Tenant?> GetBySlugAsync(string slug, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<Tenant?> GetByCustomDomainOrNullAsync(string host, CancellationToken ct = default) => Task.FromResult<Tenant?>(null);
        public Task<IReadOnlyList<Tenant>> ListAsync(CancellationToken ct = default) => Task.FromResult<IReadOnlyList<Tenant>>(Store.Values.ToList());
        public Task<bool> SlugExistsAsync(string slug, CancellationToken ct = default) => Task.FromResult(false);
        public Task AddAsync(Tenant tenant, CancellationToken ct = default) { Store[tenant.Id] = tenant; return Task.CompletedTask; }
        public Task UpdateAsync(Tenant tenant, CancellationToken ct = default) { Store[tenant.Id] = tenant; return Task.CompletedTask; }
    }

    private sealed class NullAudit : IAuditLogger
    {
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null, string? resourceType = null,
            Guid? resourceId = null, string result = "success", object? metadata = null, CancellationToken ct = default)
            => Task.CompletedTask;
    }
}
