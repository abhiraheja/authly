using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Modules.Auth;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Messaging;
using Microsoft.Extensions.Logging;

namespace Authly.Modules.AdvancedAuth;

/// <inheritdoc />
public sealed class MagicLinkService : IMagicLinkService
{
    private const string TokenType = "magic_link";
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _users;
    private readonly IVerificationTokenRepository _tokens;
    private readonly ITokenHasher _tokenHasher;
    private readonly IMessageQueue _messages;
    private readonly IAuthUrlBuilder _urls;
    private readonly IAuditLogger _audit;
    private readonly ILogger<MagicLinkService> _logger;

    public MagicLinkService(IUserRepository users, IVerificationTokenRepository tokens, ITokenHasher tokenHasher,
        IMessageQueue messages, IAuthUrlBuilder urls, IAuditLogger audit, ILogger<MagicLinkService> logger)
    {
        _users = users;
        _tokens = tokens;
        _tokenHasher = tokenHasher;
        _messages = messages;
        _urls = urls;
        _audit = audit;
        _logger = logger;
    }

    public async Task RequestAsync(Guid tenantId, string email, RequestInfo info, string? returnUrl = null, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(tenantId, Normalize(email), ct);
        if (user is null || user.Status != UserStatus.Active)
        {
            // Anti-enumeration: behave identically whether or not a usable account exists.
            _logger.LogInformation("Magic link requested for an unusable/unknown email in tenant {TenantId}.", tenantId);
            return;
        }

        await _tokens.InvalidateOutstandingAsync(user.Id, TokenType, ct);

        var raw = _tokenHasher.GenerateRawToken();
        await _tokens.AddAsync(new VerificationToken
        {
            UserId = user.Id,
            Type = TokenType,
            Target = user.Email,
            TokenHash = _tokenHasher.Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.Add(Lifetime),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        _messages.Enqueue(new MessageSendRequest(tenantId, MessageTemplateKeys.MagicLink,
            MessageChannel.Email, user.Email, new Dictionary<string, string>
            {
                ["user_name"] = NameOf(user),
                ["action_url"] = _urls.BuildMagicLinkUrl(tenantId, raw, returnUrl),
                ["expiry_minutes"] = "15"
            }));

        await _audit.LogAsync("user.magic_link_requested", new AuditContext(user.Id, "user", info.IpAddress, info.UserAgent),
            tenantId, "user", user.Id, ct: ct);
    }

    public async Task<User?> CompleteAsync(Guid tenantId, string rawToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return null;

        var token = await _tokens.GetByHashAsync(_tokenHasher.Hash(rawToken), ct);
        if (token is null || token.Used || token.Type != TokenType || token.ExpiresAt <= DateTimeOffset.UtcNow)
            return null;

        var user = await _users.GetByIdAsync(tenantId, token.UserId, ct);
        if (user is null || user.Status != UserStatus.Active)
            return null; // token belongs to another tenant, or the account is no longer usable

        token.Used = true;
        await _tokens.UpdateAsync(token, ct);

        // Opening a magic link proves control of the inbox → the email is verified.
        if (!user.EmailVerified)
        {
            user.EmailVerified = true;
            user.UpdatedAt = DateTimeOffset.UtcNow;
            await _users.UpdateAsync(user, ct);
        }

        return user;
    }

    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
    private static string NameOf(User user) => string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName!;
}
