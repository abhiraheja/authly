using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Infrastructure.Security;
using Authly.Modules.Audit;
using Authly.Modules.Auth;
using Authly.Modules.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace Authly.Tests.Auth;

public class AuthServiceTests
{
    private static readonly Guid Tenant = Guid.NewGuid();

    [Fact]
    public async Task Register_creates_hashed_user_and_queues_verification_email()
    {
        var h = new Harness();
        var user = await h.Service.RegisterAsync(Tenant,
            new RegisterRequest("New.User@Example.com", "Sup3rSecret!", "New", "User"), RequestInfo.Unknown);

        Assert.Equal("new.user@example.com", user.Email);          // normalized
        Assert.False(user.EmailVerified);
        Assert.NotNull(user.PasswordHash);
        Assert.NotEqual("Sup3rSecret!", user.PasswordHash);        // hashed, not plaintext
        Assert.Single(h.Verifications.Items);                       // a token was issued
        Assert.Single(h.Emails);                                    // an email was queued
        Assert.Contains("user.registered", h.Audit.Events);
    }

    [Fact]
    public async Task Register_rejects_duplicate_email_in_same_tenant()
    {
        var h = new Harness();
        await h.Service.RegisterAsync(Tenant, new RegisterRequest("dupe@example.com", "Sup3rSecret!"), RequestInfo.Unknown);

        await Assert.ThrowsAsync<EmailAlreadyExistsException>(() =>
            h.Service.RegisterAsync(Tenant, new RegisterRequest("dupe@example.com", "Another1!"), RequestInfo.Unknown));
    }

    [Fact]
    public async Task Authenticate_succeeds_creates_session_and_records_success()
    {
        var h = new Harness();
        await h.Service.RegisterAsync(Tenant, new RegisterRequest("a@example.com", "Sup3rSecret!"), RequestInfo.Unknown);

        var result = await h.Service.AuthenticateAsync(Tenant, "a@example.com", "Sup3rSecret!", RequestInfo.Unknown);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Session);
        Assert.NotNull(result.User!.LastLoginAt);
        Assert.Single(h.Sessions.Items);
        Assert.Contains(h.Logins.Items, l => l.Result == "success");
    }

    [Fact]
    public async Task Authenticate_with_wrong_password_records_failure()
    {
        var h = new Harness();
        await h.Service.RegisterAsync(Tenant, new RegisterRequest("a@example.com", "Sup3rSecret!"), RequestInfo.Unknown);

        var result = await h.Service.AuthenticateAsync(Tenant, "a@example.com", "wrong", RequestInfo.Unknown);

        Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);
        Assert.Empty(h.Sessions.Items);
        Assert.Contains(h.Logins.Items, l => l.Result == "failed" && l.Reason == "bad_password");
    }

    [Fact]
    public async Task Authenticate_unknown_email_records_failure_without_user()
    {
        var h = new Harness();
        var result = await h.Service.AuthenticateAsync(Tenant, "ghost@example.com", "whatever", RequestInfo.Unknown);

        Assert.Equal(LoginOutcome.InvalidCredentials, result.Outcome);
        Assert.Contains(h.Logins.Items, l => l.Result == "failed" && l.UserId is null);
    }

    [Fact]
    public async Task Authenticate_suspended_user_is_blocked()
    {
        var h = new Harness();
        var user = await h.Service.RegisterAsync(Tenant, new RegisterRequest("s@example.com", "Sup3rSecret!"), RequestInfo.Unknown);
        user.Status = UserStatus.Suspended;

        var result = await h.Service.AuthenticateAsync(Tenant, "s@example.com", "Sup3rSecret!", RequestInfo.Unknown);

        Assert.Equal(LoginOutcome.Suspended, result.Outcome);
        Assert.Contains(h.Logins.Items, l => l.Result == "blocked");
    }

    [Fact]
    public async Task VerifyEmail_consumes_token_once_then_rejects_reuse()
    {
        var h = new Harness();
        await h.Service.RegisterAsync(Tenant, new RegisterRequest("v@example.com", "Sup3rSecret!"), RequestInfo.Unknown);
        var raw = h.Urls.LastVerificationToken!;

        Assert.True(await h.Service.VerifyEmailAsync(Tenant, raw));   // first use works
        Assert.False(await h.Service.VerifyEmailAsync(Tenant, raw));  // single-use
        Assert.True(h.Users.Items.Single().EmailVerified);
    }

    [Fact]
    public async Task VerifyEmail_rejects_expired_token()
    {
        var h = new Harness();
        await h.Service.RegisterAsync(Tenant, new RegisterRequest("v@example.com", "Sup3rSecret!"), RequestInfo.Unknown);
        var raw = h.Urls.LastVerificationToken!;
        h.Verifications.Items.Single().ExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);

        Assert.False(await h.Service.VerifyEmailAsync(Tenant, raw));
    }

    [Fact]
    public async Task PasswordReset_request_for_unknown_email_is_silent()
    {
        var h = new Harness();
        await h.Service.RequestPasswordResetAsync(Tenant, "nobody@example.com", RequestInfo.Unknown);

        Assert.Empty(h.Resets.Items);
        Assert.Empty(h.Emails);
    }

    [Fact]
    public async Task PasswordReset_changes_password_revokes_sessions_and_is_single_use()
    {
        var h = new Harness();
        await h.Service.RegisterAsync(Tenant, new RegisterRequest("r@example.com", "OldP4ss!"), RequestInfo.Unknown);
        await h.Service.AuthenticateAsync(Tenant, "r@example.com", "OldP4ss!", RequestInfo.Unknown);

        await h.Service.RequestPasswordResetAsync(Tenant, "r@example.com", RequestInfo.Unknown);
        var raw = h.Urls.LastResetToken!;

        Assert.True(await h.Service.ResetPasswordAsync(Tenant, raw, "BrandN3wP!"));
        Assert.False(await h.Service.ResetPasswordAsync(Tenant, raw, "Again123!"));   // single-use

        // Active sessions revoked by the reset.
        Assert.All(h.Sessions.Items, s => Assert.True(s.Revoked));

        // New password works; old one no longer does.
        var ok = await h.Service.AuthenticateAsync(Tenant, "r@example.com", "BrandN3wP!", RequestInfo.Unknown);
        var bad = await h.Service.AuthenticateAsync(Tenant, "r@example.com", "OldP4ss!", RequestInfo.Unknown);
        Assert.True(ok.Succeeded);
        Assert.False(bad.Succeeded);
    }

    // --- test harness with in-memory fakes + real crypto ---

    private sealed class Harness
    {
        public readonly FakeUserRepository Users = new();
        public readonly FakeSessionRepository Sessions = new();
        public readonly FakeLoginHistoryRepository Logins = new();
        public readonly FakeVerificationRepository Verifications = new();
        public readonly FakeResetRepository Resets = new();
        public readonly RecordingAuditLogger Audit = new();
        public readonly FakeUrlBuilder Urls = new();
        public readonly List<EmailMessage> Emails = new();
        public readonly AuthService Service;

        public Harness()
        {
            var emailQueue = new CapturingEmailQueue(Emails);
            Service = new AuthService(Users, Sessions, Logins, Verifications, Resets,
                new Argon2idPasswordHasher(), new Sha256TokenHasher(),
                emailQueue, Urls, Audit, NullLogger<AuthService>.Instance);
        }
    }

    private sealed class FakeUserRepository : IUserRepository
    {
        public readonly List<User> Items = new();
        public Task<User?> GetByIdAsync(Guid tenantId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(u => u.TenantId == tenantId && u.Id == id));
        public Task<User?> GetByEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(u => u.TenantId == tenantId && u.Email == email));
        public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken ct = default)
            => Task.FromResult(Items.Any(u => u.TenantId == tenantId && u.Email == email));
        public Task AddAsync(User user, CancellationToken ct = default)
        {
            if (user.Id == Guid.Empty) user.Id = Guid.NewGuid();
            Items.Add(user);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeSessionRepository : ISessionRepository
    {
        public readonly List<Session> Items = new();
        public Task<Session?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s => s.Id == id));
        public Task<Session?> GetByRefreshTokenHashAsync(string hash, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s => s.RefreshTokenHash == hash));
        public Task<IReadOnlyList<Session>> ListActiveForUserAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Session>>(
                Items.Where(s => s.TenantId == tenantId && s.UserId == userId && !s.Revoked && s.ExpiresAt > DateTimeOffset.UtcNow).ToList());
        public Task AddAsync(Session session, CancellationToken ct = default)
        {
            if (session.Id == Guid.Empty) session.Id = Guid.NewGuid();
            Items.Add(session);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(Session session, CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeLoginHistoryRepository : ILoginHistoryRepository
    {
        public readonly List<LoginHistory> Items = new();
        public Task AddAsync(LoginHistory entry, CancellationToken ct = default)
        {
            Items.Add(entry);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<LoginHistory>> ListForUserAsync(Guid tenantId, Guid userId, int limit = 50, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<LoginHistory>>(
                Items.Where(h => h.TenantId == tenantId && h.UserId == userId).ToList());
    }

    private sealed class FakeVerificationRepository : IVerificationTokenRepository
    {
        public readonly List<VerificationToken> Items = new();
        public Task<VerificationToken?> GetByHashAsync(string hash, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == hash));
        public Task AddAsync(VerificationToken token, CancellationToken ct = default)
        {
            if (token.Id == Guid.Empty) token.Id = Guid.NewGuid();
            Items.Add(token);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(VerificationToken token, CancellationToken ct = default) => Task.CompletedTask;
        public Task InvalidateOutstandingAsync(Guid userId, string type, CancellationToken ct = default)
        {
            foreach (var t in Items.Where(t => t.UserId == userId && t.Type == type && !t.Used)) t.Used = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeResetRepository : IPasswordResetTokenRepository
    {
        public readonly List<PasswordResetToken> Items = new();
        public Task<PasswordResetToken?> GetByHashAsync(string hash, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(t => t.TokenHash == hash));
        public Task AddAsync(PasswordResetToken token, CancellationToken ct = default)
        {
            if (token.Id == Guid.Empty) token.Id = Guid.NewGuid();
            Items.Add(token);
            return Task.CompletedTask;
        }
        public Task UpdateAsync(PasswordResetToken token, CancellationToken ct = default) => Task.CompletedTask;
        public Task InvalidateOutstandingAsync(Guid userId, CancellationToken ct = default)
        {
            foreach (var t in Items.Where(t => t.UserId == userId && !t.Used)) t.Used = true;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingAuditLogger : IAuditLogger
    {
        public readonly List<string> Events = new();
        public Task LogAsync(string @event, AuditContext actor, Guid? tenantId = null,
            string? resourceType = null, Guid? resourceId = null, string result = "success",
            object? metadata = null, CancellationToken ct = default)
        {
            Events.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUrlBuilder : IAuthUrlBuilder
    {
        public string? LastVerificationToken;
        public string? LastResetToken;
        public string BuildEmailVerificationUrl(Guid tenantId, string rawToken)
        {
            LastVerificationToken = rawToken;
            return $"https://test/verify?token={rawToken}";
        }
        public string BuildPasswordResetUrl(Guid tenantId, string rawToken)
        {
            LastResetToken = rawToken;
            return $"https://test/reset?token={rawToken}";
        }
    }

    private sealed class CapturingEmailQueue : IEmailQueue
    {
        private readonly List<EmailMessage> _sink;
        public CapturingEmailQueue(List<EmailMessage> sink) => _sink = sink;
        public void Queue(EmailMessage message) => _sink.Add(message);
    }
}
