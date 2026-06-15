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
public sealed class RecoveryService : IRecoveryService
{
    private static readonly TimeSpan RecoveryTokenLifetime = TimeSpan.FromHours(1);

    private readonly IRecoveryContactRepository _contacts;
    private readonly IUserRepository _users;
    private readonly IPasswordResetTokenRepository _resetTokens;
    private readonly ITokenHasher _tokenHasher;
    private readonly IMessageQueue _messages;
    private readonly IAuthUrlBuilder _urls;
    private readonly IAuditLogger _audit;
    private readonly ILogger<RecoveryService> _logger;

    public RecoveryService(IRecoveryContactRepository contacts, IUserRepository users,
        IPasswordResetTokenRepository resetTokens, ITokenHasher tokenHasher, IMessageQueue messages,
        IAuthUrlBuilder urls, IAuditLogger audit, ILogger<RecoveryService> logger)
    {
        _contacts = contacts;
        _users = users;
        _resetTokens = resetTokens;
        _tokenHasher = tokenHasher;
        _messages = messages;
        _urls = urls;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RecoveryContactSummary>> ListContactsAsync(Guid tenantId, Guid userId, CancellationToken ct = default)
        => (await _contacts.ListByUserAsync(tenantId, userId, ct))
            .Select(c => new RecoveryContactSummary(c.Id, c.Type, c.Value, c.Verified, c.CreatedAt))
            .ToList();

    public async Task AddContactAsync(Guid tenantId, Guid userId, ContactType type, string value, AuditContext actor, CancellationToken ct = default)
    {
        value = type == ContactType.Email ? value.Trim().ToLowerInvariant() : value.Trim();
        if (string.IsNullOrWhiteSpace(value))
            throw new AdvancedAuthException("Enter a contact value.");

        var existing = await _contacts.ListByUserAsync(tenantId, userId, ct);
        if (existing.Any(c => c.Type == type && string.Equals(c.Value, value, StringComparison.OrdinalIgnoreCase)))
            return; // idempotent — already present

        await _contacts.AddAsync(new RecoveryContact
        {
            TenantId = tenantId,
            UserId = userId,
            Type = type,
            Value = value,
            Verified = false,
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        await _audit.LogAsync("user.recovery_contact_added", actor, tenantId, "user", userId,
            metadata: new { type = type.ToString().ToLowerInvariant() }, ct: ct);
    }

    public async Task RemoveContactAsync(Guid tenantId, Guid userId, Guid contactId, AuditContext actor, CancellationToken ct = default)
    {
        var contact = await _contacts.GetByIdAsync(tenantId, contactId, ct);
        if (contact is null || contact.UserId != userId) return; // ownership guard

        await _contacts.DeleteAsync(contact, ct);
        await _audit.LogAsync("user.recovery_contact_removed", actor, tenantId, "user", userId, ct: ct);
    }

    public async Task InitiateRecoveryAsync(Guid tenantId, string email, RequestInfo info, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(tenantId, email.Trim().ToLowerInvariant(), ct);
        if (user is null)
        {
            // Anti-enumeration: identical behaviour whether or not the account exists.
            _logger.LogInformation("Account recovery requested for an unknown email in tenant {TenantId}.", tenantId);
            return;
        }

        // Issue a single recovery (reset) token; the link lets the user set a new password.
        await _resetTokens.InvalidateOutstandingAsync(user.Id, ct);
        var raw = _tokenHasher.GenerateRawToken();
        await _resetTokens.AddAsync(new PasswordResetToken
        {
            UserId = user.Id,
            TokenHash = _tokenHasher.Hash(raw),
            ExpiresAt = DateTimeOffset.UtcNow.Add(RecoveryTokenLifetime),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        var url = _urls.BuildRecoveryUrl(tenantId, raw);
        var name = NameOf(user);

        // Notify EVERY recovery channel: the account email plus all recovery contacts.
        _messages.Enqueue(new MessageSendRequest(tenantId, MessageTemplateKeys.AccountRecovery,
            MessageChannel.Email, user.Email, new Dictionary<string, string>
            {
                ["user_name"] = name, ["action_url"] = url, ["expiry_hours"] = "1"
            }));

        foreach (var contact in await _contacts.ListByUserAsync(tenantId, user.Id, ct))
        {
            var channel = contact.Type == ContactType.Email ? MessageChannel.Email : MessageChannel.WhatsApp;
            _messages.Enqueue(new MessageSendRequest(tenantId, MessageTemplateKeys.AccountRecovery,
                channel, contact.Value, new Dictionary<string, string>
                {
                    ["user_name"] = name, ["action_url"] = url, ["expiry_hours"] = "1"
                }));
        }

        await _audit.LogAsync("user.recovery_initiated", new AuditContext(user.Id, "user", info.IpAddress, info.UserAgent),
            tenantId, "user", user.Id, ct: ct);
    }

    private static string NameOf(User user) => string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName!;
}
