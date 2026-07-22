using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Messaging;
using Microsoft.Extensions.Logging;

namespace Authly.Modules.Auth;

/// <inheritdoc />
public sealed class AuthService : IAuthService
{
    // Token/session lifetimes. Kept conservative; promote to options when tenants configure them.
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromHours(24);
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(8);

    private readonly IUserRepository _users;
    private readonly ISessionRepository _sessions;
    private readonly ILoginHistoryRepository _loginHistory;
    private readonly IVerificationTokenRepository _verificationTokens;
    private readonly IPasswordResetTokenRepository _resetTokens;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenHasher _tokenHasher;
    private readonly IMessageQueue _messages;
    private readonly IAuthUrlBuilder _urls;
    private readonly IAuditLogger _audit;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        ISessionRepository sessions,
        ILoginHistoryRepository loginHistory,
        IVerificationTokenRepository verificationTokens,
        IPasswordResetTokenRepository resetTokens,
        IPasswordHasher passwordHasher,
        ITokenHasher tokenHasher,
        IMessageQueue messages,
        IAuthUrlBuilder urls,
        IAuditLogger audit,
        ILogger<AuthService> logger)
    {
        _users = users;
        _sessions = sessions;
        _loginHistory = loginHistory;
        _verificationTokens = verificationTokens;
        _resetTokens = resetTokens;
        _passwordHasher = passwordHasher;
        _tokenHasher = tokenHasher;
        _messages = messages;
        _urls = urls;
        _audit = audit;
        _logger = logger;
    }

    public async Task<User> RegisterAsync(Guid tenantId, RegisterRequest request, RequestInfo info, CancellationToken ct = default)
    {
        var email = Normalize(request.Email);

        if (await _users.EmailExistsAsync(tenantId, email, ct))
            throw new EmailAlreadyExistsException(email);

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            TenantId = tenantId,
            Email = email,
            EmailVerified = false,
            PasswordHash = _passwordHasher.Hash(request.Password),
            Status = UserStatus.Active,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = now,
            UpdatedAt = now
        };
        await _users.AddAsync(user, ct);

        await IssueVerificationEmailAsync(user, ct);
        // Non-sensitive user fields ride along on the event so webhook subscribers can provision
        // downstream records without a callback. Never include secrets/credentials here.
        await _audit.LogAsync("user.registered", Actor(user.Id, info), tenantId,
            resourceType: "user", resourceId: user.Id,
            metadata: new { email = user.Email, firstName = user.FirstName, lastName = user.LastName, phone = user.Phone, avatarUrl = user.AvatarUrl },
            ct: ct);

        return user;
    }

    public async Task<LoginResult> AuthenticateAsync(Guid tenantId, string email, string password, RequestInfo info, CancellationToken ct = default)
    {
        email = Normalize(email);
        var user = await _users.GetByEmailAsync(tenantId, email, ct);
        return await CompletePasswordLoginAsync(tenantId, user, password, "password", info, ct);
    }

    public async Task<LoginResult> AuthenticateByPhoneAsync(Guid tenantId, string phone, string password, RequestInfo info, CancellationToken ct = default)
    {
        var normalized = Authly.Modules.Common.PhoneNumber.Normalize(phone) ?? string.Empty;
        var user = string.IsNullOrEmpty(normalized) ? null : await _users.GetByVerifiedPhoneAsync(tenantId, normalized, ct);
        return await CompletePasswordLoginAsync(tenantId, user, password, "password_phone", info, ct);
    }

    /// <summary>Shared password-login completion once the user has been resolved (by email or phone):
    /// status check, password verify, session issue + login history. <paramref name="method"/> tags
    /// the channel in login history.</summary>
    private async Task<LoginResult> CompletePasswordLoginAsync(Guid tenantId, User? user, string password,
        string method, RequestInfo info, CancellationToken ct)
    {
        if (user is null)
        {
            await RecordLoginAsync(tenantId, null, "failed", "unknown_user", method, info, ct);
            return new LoginResult(LoginOutcome.InvalidCredentials);
        }

        if (user.Status is UserStatus.Suspended or UserStatus.Deleted)
        {
            await RecordLoginAsync(tenantId, user.Id, "blocked", $"status_{user.Status}".ToLowerInvariant(), method, info, ct);
            await _audit.LogAsync("user.login", Actor(user.Id, info), tenantId, "user", user.Id,
                result: "failure", metadata: new { reason = "blocked" }, ct: ct);
            return new LoginResult(LoginOutcome.Suspended, user);
        }

        if (string.IsNullOrEmpty(user.PasswordHash) || !_passwordHasher.Verify(user.PasswordHash, password))
        {
            await RecordLoginAsync(tenantId, user.Id, "failed", "bad_password", method, info, ct);
            await _audit.LogAsync("user.login", Actor(user.Id, info), tenantId, "user", user.Id,
                result: "failure", metadata: new { reason = "bad_password" }, ct: ct);
            return new LoginResult(LoginOutcome.InvalidCredentials);
        }

        var session = await StartSessionAsync(user, method, info, ct);
        return new LoginResult(LoginOutcome.Success, user, session);
    }

    public async Task<Session> StartSessionAsync(User user, string method, RequestInfo info, CancellationToken ct = default)
    {
        var session = await CreateSessionAsync(user, info, ct);

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user, ct);

        await RecordLoginAsync(user.TenantId, user.Id, "success", null, method, info, ct);
        await _audit.LogAsync("user.login", Actor(user.Id, info), user.TenantId, "user", user.Id,
            metadata: new { method }, ct: ct);

        return session;
    }

    public async Task ResendVerificationEmailAsync(Guid tenantId, string email, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(tenantId, Normalize(email), ct);
        if (user is null || user.EmailVerified)
            return; // nothing to do; stay silent either way

        await IssueVerificationEmailAsync(user, ct);
    }

    public async Task<bool> VerifyEmailAsync(Guid tenantId, string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return false;

        var token = await _verificationTokens.GetByHashAsync(_tokenHasher.Hash(rawToken), ct);
        if (token is null || token.Used || token.Type != "email" || token.ExpiresAt <= DateTimeOffset.UtcNow)
            return false;

        var user = await _users.GetByIdAsync(tenantId, token.UserId, ct);
        if (user is null)
            return false; // token belongs to another tenant or user no longer exists

        token.Used = true;
        await _verificationTokens.UpdateAsync(token, ct);

        if (!user.EmailVerified)
        {
            user.EmailVerified = true;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _users.UpdateAsync(user, ct);
        }

        await _audit.LogAsync("user.email_verified", Actor(user.Id, RequestInfo.Unknown), user.TenantId,
            "user", user.Id, ct: ct);
        return true;
    }

    public async Task RequestPasswordResetAsync(Guid tenantId, string email, RequestInfo info, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(tenantId, Normalize(email), ct);
        if (user is null)
        {
            // Anti-enumeration: behave identically whether or not the email exists.
            _logger.LogInformation("Password reset requested for an unknown email in tenant {TenantId}.", tenantId);
            return;
        }

        await _resetTokens.InvalidateOutstandingAsync(user.Id, ct);

        var raw = _tokenHasher.GenerateRawToken();
        await _resetTokens.AddAsync(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = _tokenHasher.Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.Add(ResetTokenLifetime),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        var url = await _urls.BuildPasswordResetUrl(tenantId, raw);
        _messages.Enqueue(new MessageSendRequest(tenantId, MessageTemplateKeys.ResetPassword,
            MessageChannel.Email, user.Email, new Dictionary<string, string>
            {
                ["user_name"] = NameOf(user),
                ["action_url"] = url,
                ["expiry_hours"] = "1"
            }));

        await _audit.LogAsync("user.password_reset_requested", Actor(user.Id, info), tenantId,
            "user", user.Id, ct: ct);
    }

    public async Task<bool> ResetPasswordAsync(Guid tenantId, string rawToken, string newPassword, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
            return false;

        var token = await _resetTokens.GetByHashAsync(_tokenHasher.Hash(rawToken), ct);
        if (token is null || token.Used || token.ExpiresAt <= DateTimeOffset.UtcNow)
            return false;

        var user = await _users.GetByIdAsync(tenantId, token.UserId, ct);
        if (user is null)
            return false; // token belongs to another tenant or user no longer exists

        token.Used = true;
        await _resetTokens.UpdateAsync(token, ct);

        user.PasswordHash = _passwordHasher.Hash(newPassword);
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user, ct);

        // A password change invalidates every existing session.
        await RevokeAllSessionsAsync(user, ct);

        await _audit.LogAsync("user.password_reset", Actor(user.Id, RequestInfo.Unknown), user.TenantId,
            "user", user.Id, ct: ct);
        return true;
    }

    public async Task<Session?> GetActiveSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, ct);
        if (session is null || session.Revoked || session.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;
        return session;
    }

    public async Task RevokeSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, ct);
        if (session is null || session.Revoked)
            return;
        session.Revoked = true;
        await _sessions.UpdateAsync(session, ct);
    }

    public async Task TouchSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _sessions.GetByIdAsync(sessionId, ct);
        if (session is null || session.Revoked)
            return; // never slide a revoked/expired-away row back to life
        var now = DateTimeOffset.UtcNow;
        session.LastActiveAt = now;
        session.ExpiresAt = now.Add(SessionLifetime);
        await _sessions.UpdateAsync(session, ct);
    }

    // --- helpers ---

    private async Task<Session> CreateSessionAsync(User user, RequestInfo info, CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        // Cookie sessions still get an opaque secret so the sessions table is uniform with
        // OAuth refresh sessions (Phase 3); only its SHA-256 hash is stored.
        var raw = _tokenHasher.GenerateRawToken();
        var session = new Session
        {
            UserId = user.Id,
            TenantId = user.TenantId,
            RefreshTokenHash = _tokenHasher.Hash(raw),
            RefreshFamilyId = Guid.NewGuid(),
            IpAddress = info.IpAddress,
            UserAgent = info.UserAgent,
            DeviceFingerprint = info.Device,
            LastActiveAt = now,
            ExpiresAt = now.Add(SessionLifetime),
            CreatedAt = now
        };
        await _sessions.AddAsync(session, ct);
        return session;
    }

    private async Task RevokeAllSessionsAsync(User user, CancellationToken ct)
    {
        foreach (var session in await _sessions.ListActiveForUserAsync(user.TenantId, user.Id, ct))
        {
            session.Revoked = true;
            await _sessions.UpdateAsync(session, ct);
        }
    }

    private async Task IssueVerificationEmailAsync(User user, CancellationToken ct)
    {
        await _verificationTokens.InvalidateOutstandingAsync(user.Id, "email", ct);

        var raw = _tokenHasher.GenerateRawToken();
        await _verificationTokens.AddAsync(new VerificationToken
        {
            UserId = user.Id,
            Type = "email",
            Target = user.Email,
            TokenHash = _tokenHasher.Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.Add(VerificationTokenLifetime),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        var url = await _urls.BuildEmailVerificationUrl(user.TenantId, raw);
        _messages.Enqueue(new MessageSendRequest(user.TenantId, MessageTemplateKeys.VerifyEmail,
            MessageChannel.Email, user.Email, new Dictionary<string, string>
            {
                ["user_name"] = NameOf(user),
                ["action_url"] = url,
                ["expiry_hours"] = "24"
            }));
    }

    private async Task RecordLoginAsync(Guid tenantId, Guid? userId, string result, string? reason, string method, RequestInfo info, CancellationToken ct)
        => await _loginHistory.AddAsync(new LoginHistory
        {
            TenantId = tenantId,
            UserId = userId,
            Result = result,
            Method = method,
            Reason = reason,
            IpAddress = info.IpAddress,
            UserAgent = info.UserAgent,
            Device = info.Device,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

    private static AuditContext Actor(Guid userId, RequestInfo info)
        => new(userId, "user", info.IpAddress, info.UserAgent);

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();

    private static string NameOf(User user) => string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName!;
}
