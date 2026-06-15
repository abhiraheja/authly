using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.OAuth;
using Authly.Infrastructure.Security;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Authly.Modules.Social;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Authly.Tests.Social;

public class SocialLoginServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();
    private const string GoogleProfile = """{"sub":"g-123","email":"ada@example.com","email_verified":true,"name":"Ada"}""";

    [Fact]
    public async Task Jit_creates_a_verified_social_only_user()
    {
        var h = new Harness();
        h.ConfigureGoogle();

        var result = await h.Service.CompleteLoginAsync(Tenant, "google", "code", "https://x/cb", RequestInfo.Unknown);

        Assert.True(result.IsNewUser);
        Assert.False(result.Linked);
        var user = Assert.Single(h.Users.Items);
        Assert.Equal("ada@example.com", user.Email);
        Assert.True(user.EmailVerified);
        Assert.Null(user.PasswordHash);                      // social-only
        var identity = Assert.Single(h.Identities.Items);
        Assert.Equal("g-123", identity.ProviderId);
    }

    [Fact]
    public async Task Links_to_an_existing_email_account()
    {
        var h = new Harness();
        h.ConfigureGoogle();
        var existing = new User { Id = Guid.NewGuid(), TenantId = Tenant, Email = "ada@example.com", PasswordHash = "argon" };
        h.Users.Items.Add(existing);

        var result = await h.Service.CompleteLoginAsync(Tenant, "google", "code", "https://x/cb", RequestInfo.Unknown);

        Assert.False(result.IsNewUser);
        Assert.True(result.Linked);
        Assert.Equal(existing.Id, result.User.Id);           // merged, not duplicated
        Assert.Single(h.Users.Items);
        Assert.Equal(existing.Id, h.Identities.Items.Single().UserId);
    }

    [Fact]
    public async Task Returning_user_reuses_the_existing_identity()
    {
        var h = new Harness();
        h.ConfigureGoogle();
        var user = new User { Id = Guid.NewGuid(), TenantId = Tenant, Email = "ada@example.com" };
        h.Users.Items.Add(user);
        h.Identities.Items.Add(new SocialIdentity
        {
            Id = Guid.NewGuid(), TenantId = Tenant, UserId = user.Id, Provider = "google", ProviderId = "g-123"
        });

        var result = await h.Service.CompleteLoginAsync(Tenant, "google", "code", "https://x/cb", RequestInfo.Unknown);

        Assert.False(result.IsNewUser);
        Assert.False(result.Linked);
        Assert.Equal(user.Id, result.User.Id);
        Assert.Single(h.Identities.Items);                   // updated, not duplicated
    }

    [Fact]
    public async Task Provider_tokens_are_encrypted_at_rest()
    {
        var h = new Harness();
        h.ConfigureGoogle();
        h.Gateway.Tokens = new SocialTokenSet("access-xyz", "refresh-abc", DateTimeOffset.UtcNow.AddHours(1), null);

        await h.Service.CompleteLoginAsync(Tenant, "google", "code", "https://x/cb", RequestInfo.Unknown);

        var identity = Assert.Single(h.Identities.Items);
        Assert.NotEqual("access-xyz", identity.AccessToken);
        Assert.Equal("access-xyz", h.Encryption.Decrypt(identity.AccessToken!));
        Assert.Equal("refresh-abc", h.Encryption.Decrypt(identity.RefreshToken!));
    }

    [Fact]
    public async Task Unverified_provider_email_is_rejected()
    {
        var h = new Harness();
        h.ConfigureGoogle();
        h.Gateway.UserInfoJson = """{"sub":"g-9","email":"ada@example.com","email_verified":false,"name":"Ada"}""";

        await Assert.ThrowsAsync<SocialProfileMissingEmailException>(() =>
            h.Service.CompleteLoginAsync(Tenant, "google", "code", "https://x/cb", RequestInfo.Unknown));
        Assert.Empty(h.Users.Items);
    }

    [Fact]
    public async Task Unconfigured_provider_cannot_start_a_login()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<SocialProviderNotConfiguredException>(() =>
            h.Service.BuildAuthorizationUrlAsync(Tenant, "google", "https://x/cb", "state"));
    }

    [Fact]
    public async Task Saving_a_custom_provider_without_endpoints_is_rejected()
    {
        var h = new Harness();
        await Assert.ThrowsAsync<SocialProviderConfigInvalidException>(() =>
            h.Service.SaveProviderAsync(Tenant, new SocialProviderInput
            {
                Provider = "custom", ClientId = "abc", IsActive = true
            }, AuditContext.System));
    }

    // --- harness ------------------------------------------------------------

    private sealed class Harness
    {
        public readonly FakeProviderRepo Providers = new();
        public readonly FakeIdentityRepo Identities = new();
        public readonly FakeUserRepo Users = new();
        public readonly FakeAuth Auth = new();
        public readonly FakeGateway Gateway = new();
        public readonly AesEncryptionService Encryption =
            new(Options.Create(new EncryptionOptions { Key = "3J8mZ1qg9X0vQpYb2sR7tU4wK6nL5cD8eF1aH0iJ2kM=" }));
        public readonly SocialLoginService Service;

        public Harness()
        {
            Gateway.UserInfoJson = GoogleProfile;
            Service = new SocialLoginService(Providers, Identities, Users, Auth, Gateway, Encryption,
                new RecordingAuditLogger(), NullLogger<SocialLoginService>.Instance);
        }

        public void ConfigureGoogle() => Providers.Items.Add(new SocialProvider
        {
            Id = Guid.NewGuid(), TenantId = Tenant, Provider = "google", ClientId = "cid", IsActive = true
        });
    }

    private sealed class FakeGateway : ISocialAuthGateway
    {
        public string UserInfoJson = "{}";
        public SocialTokenSet Tokens = new("access", "refresh", null, null);
        public string BuildAuthorizationUrl(SocialAuthConfig c, string redirectUri, string state) => $"{c.AuthorizationEndpoint}?state={state}";
        public Task<SocialTokenSet> ExchangeCodeAsync(SocialAuthConfig c, string code, string redirectUri, CancellationToken ct = default) => Task.FromResult(Tokens);
        public Task<string> FetchUserInfoJsonAsync(SocialAuthConfig c, SocialTokenSet tokens, CancellationToken ct = default) => Task.FromResult(UserInfoJson);
    }

    private sealed class FakeAuth : IAuthService
    {
        public Task<Session> StartSessionAsync(User user, string method, RequestInfo info, CancellationToken ct = default)
            => Task.FromResult(new Session { Id = Guid.NewGuid(), UserId = user.Id, TenantId = user.TenantId, ExpiresAt = DateTimeOffset.UtcNow.AddHours(1) });
        public Task<User> RegisterAsync(Guid t, RegisterRequest r, RequestInfo i, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<LoginResult> AuthenticateAsync(Guid t, string e, string p, RequestInfo i, CancellationToken ct = default) => throw new NotImplementedException();
        public Task ResendVerificationEmailAsync(Guid t, string e, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> VerifyEmailAsync(Guid t, string tok, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RequestPasswordResetAsync(Guid t, string e, RequestInfo i, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<bool> ResetPasswordAsync(Guid t, string tok, string pw, CancellationToken ct = default) => throw new NotImplementedException();
        public Task<Session?> GetActiveSessionAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
        public Task RevokeSessionAsync(Guid id, CancellationToken ct = default) => throw new NotImplementedException();
    }

    private sealed class FakeProviderRepo : ISocialProviderRepository
    {
        public readonly List<SocialProvider> Items = new();
        public Task<IReadOnlyList<SocialProvider>> ListByTenantAsync(Guid t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SocialProvider>>(Items.Where(p => p.TenantId == t).ToList());
        public Task<SocialProvider?> GetAsync(Guid t, string provider, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Provider == provider));
        public Task<SocialProvider?> GetByIdAsync(Guid t, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(p => p.TenantId == t && p.Id == id));
        public Task AddAsync(SocialProvider p, CancellationToken ct = default) { if (p.Id == Guid.Empty) p.Id = Guid.NewGuid(); Items.Add(p); return Task.CompletedTask; }
        public Task UpdateAsync(SocialProvider p, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(SocialProvider p, CancellationToken ct = default) { Items.Remove(p); return Task.CompletedTask; }
    }

    private sealed class FakeIdentityRepo : ISocialIdentityRepository
    {
        public readonly List<SocialIdentity> Items = new();
        public Task<SocialIdentity?> GetAsync(Guid t, string provider, string providerId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s => s.TenantId == t && s.Provider == provider && s.ProviderId == providerId));
        public Task<IReadOnlyList<SocialIdentity>> ListByUserAsync(Guid t, Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SocialIdentity>>(Items.Where(s => s.TenantId == t && s.UserId == userId).ToList());
        public Task AddAsync(SocialIdentity s, CancellationToken ct = default) { if (s.Id == Guid.Empty) s.Id = Guid.NewGuid(); Items.Add(s); return Task.CompletedTask; }
        public Task UpdateAsync(SocialIdentity s, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeUserRepo : IUserRepository
    {
        public readonly List<User> Items = new();
        public Task<User?> GetByIdAsync(Guid t, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(u => u.TenantId == t && u.Id == id));
        public Task<User?> GetByEmailAsync(Guid t, string email, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(u => u.TenantId == t && u.Email == email));
        public Task<bool> EmailExistsAsync(Guid t, string email, CancellationToken ct = default)
            => Task.FromResult(Items.Any(u => u.TenantId == t && u.Email == email));
        public Task<IReadOnlyList<User>> ListByTenantAsync(Guid t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<User>>(Items.Where(u => u.TenantId == t).ToList());
        public Task<Core.Common.PagedResult<User>> ListPagedAsync(Guid t, Core.Common.Pagination page, string? emailContains = null, CancellationToken ct = default)
            => throw new NotImplementedException();
        public Task DeleteAsync(User user, CancellationToken ct = default) { Items.Remove(user); return Task.CompletedTask; }
        public Task<bool> AnyTenantAdminAsync(Guid t, CancellationToken ct = default) => Task.FromResult(false);
        public Task AddAsync(User user, CancellationToken ct = default) { if (user.Id == Guid.Empty) user.Id = Guid.NewGuid(); Items.Add(user); return Task.CompletedTask; }
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null,
            string? resourceType = null, Guid? resourceId = null, string result = "success",
            object? metadata = null, CancellationToken ct = default) => Task.CompletedTask;
    }
}
