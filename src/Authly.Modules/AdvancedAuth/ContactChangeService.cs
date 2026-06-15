using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Messaging;
using Authly.Modules.Auth;
using Authly.Modules.Audit;
using Authly.Modules.Common;
using Authly.Modules.Messaging;

namespace Authly.Modules.AdvancedAuth;

/// <inheritdoc />
public sealed class ContactChangeService : IContactChangeService
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);
    private static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(2);

    private readonly IUserRepository _users;
    private readonly IPendingContactChangeRepository _changes;
    private readonly ITokenHasher _tokenHasher;
    private readonly IMessageQueue _messages;
    private readonly IAuthUrlBuilder _urls;
    private readonly IAuditLogger _audit;

    public ContactChangeService(IUserRepository users, IPendingContactChangeRepository changes, ITokenHasher tokenHasher,
        IMessageQueue messages, IAuthUrlBuilder urls, IAuditLogger audit)
    {
        _users = users;
        _changes = changes;
        _tokenHasher = tokenHasher;
        _messages = messages;
        _urls = urls;
        _audit = audit;
    }

    public async Task<ContactChangeOutcome> RequestChangeAsync(Guid tenantId, Guid userId, ContactType type, string newValue,
        RequestInfo info, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(tenantId, userId, ct)
            ?? throw new AdvancedAuthException("User not found.");

        newValue = type == ContactType.Email ? Normalize(newValue) : newValue.Trim();
        if (string.IsNullOrWhiteSpace(newValue))
            throw new AdvancedAuthException("Enter the new contact value.");

        if (type == ContactType.Email && await _users.EmailExistsAsync(tenantId, newValue, ct))
            return ContactChangeOutcome.AlreadyInUse;

        // Cooldown: don't let a fresh pending request be re-issued repeatedly.
        var existing = await _changes.GetPendingByUserAsync(tenantId, userId, ct);
        if (existing is not null)
        {
            if (DateTimeOffset.UtcNow - existing.CreatedAt < Cooldown)
                return ContactChangeOutcome.Cooldown;
            existing.Status = ContactChangeStatus.Cancelled;
            await _changes.UpdateAsync(existing, ct);
        }

        var rawVerify = _tokenHasher.GenerateRawToken();
        var rawCancel = _tokenHasher.GenerateRawToken();
        await _changes.AddAsync(new PendingContactChange
        {
            TenantId = tenantId,
            UserId = userId,
            ChangeType = type,
            NewValue = newValue,
            VerifyTokenHash = _tokenHasher.Hash(rawVerify),
            CancelTokenHash = _tokenHasher.Hash(rawCancel),
            Status = ContactChangeStatus.Pending,
            ExpiresAt = DateTimeOffset.UtcNow.Add(Lifetime),
            CreatedAt = DateTimeOffset.UtcNow
        }, ct);

        // 1) Confirmation to the NEW contact (email link, or WhatsApp link for a phone).
        var verifyUrl = _urls.BuildContactChangeVerifyUrl(tenantId, rawVerify);
        var name = NameOf(user);
        if (type == ContactType.Email)
        {
            _messages.Enqueue(new MessageSendRequest(tenantId, MessageTemplateKeys.VerifyNewContact,
                MessageChannel.Email, newValue, new Dictionary<string, string>
                {
                    ["user_name"] = name, ["action_url"] = verifyUrl, ["expiry_hours"] = "1"
                }));
        }
        else
        {
            _messages.Enqueue(new MessageSendRequest(tenantId, MessageTemplateKeys.VerifyNewContact,
                MessageChannel.WhatsApp, newValue, new Dictionary<string, string>
                {
                    ["user_name"] = name, ["action_url"] = verifyUrl, ["expiry_hours"] = "1"
                }));
        }

        // 2) Alert the OLD contact (the account email) with a cancel link.
        _messages.Enqueue(new MessageSendRequest(tenantId, MessageTemplateKeys.ContactChangeAlert,
            MessageChannel.Email, user.Email, new Dictionary<string, string>
            {
                ["user_name"] = name,
                ["contact_type"] = type == ContactType.Email ? "email address" : "phone number",
                ["action_url"] = _urls.BuildContactChangeCancelUrl(tenantId, rawCancel)
            }));

        await _audit.LogAsync("user.contact_change_requested", Actor(userId, info), tenantId,
            "user", userId, metadata: new { type = type.ToString().ToLowerInvariant() }, ct: ct);

        return ContactChangeOutcome.Started;
    }

    public async Task<bool> VerifyAsync(Guid tenantId, string rawVerifyToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawVerifyToken)) return false;

        var change = await _changes.GetByVerifyHashAsync(_tokenHasher.Hash(rawVerifyToken), ct);
        if (change is null || change.Status != ContactChangeStatus.Pending || change.ExpiresAt <= DateTimeOffset.UtcNow)
            return false;

        var user = await _users.GetByIdAsync(change.TenantId, change.UserId, ct);
        if (user is null) return false;

        // Email uniqueness may have changed since the request — re-check at apply time.
        if (change.ChangeType == ContactType.Email && await _users.EmailExistsAsync(change.TenantId, change.NewValue, ct))
            return false;

        if (change.ChangeType == ContactType.Email)
        {
            user.Email = change.NewValue;
            user.EmailVerified = true;
        }
        else
        {
            user.Phone = change.NewValue;
            user.PhoneVerified = true;
        }
        user.UpdatedAt = DateTimeOffset.UtcNow;
        await _users.UpdateAsync(user, ct);

        change.Status = ContactChangeStatus.Completed;
        await _changes.UpdateAsync(change, ct);

        await _audit.LogAsync(change.ChangeType == ContactType.Email ? "user.email_changed" : "user.phone_changed",
            new AuditContext(user.Id, "user"), change.TenantId, "user", user.Id, ct: ct);
        return true;
    }

    public async Task<bool> CancelAsync(string rawCancelToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawCancelToken)) return false;

        var change = await _changes.GetByCancelHashAsync(_tokenHasher.Hash(rawCancelToken), ct);
        if (change is null || change.Status != ContactChangeStatus.Pending)
            return false;

        change.Status = ContactChangeStatus.Cancelled;
        await _changes.UpdateAsync(change, ct);

        await _audit.LogAsync("user.contact_change_cancelled", new AuditContext(change.UserId, "user"),
            change.TenantId, "user", change.UserId, ct: ct);
        return true;
    }

    private static AuditContext Actor(Guid userId, RequestInfo info) => new(userId, "user", info.IpAddress, info.UserAgent);
    private static string Normalize(string email) => email.Trim().ToLowerInvariant();
    private static string NameOf(User user) => string.IsNullOrWhiteSpace(user.FirstName) ? "there" : user.FirstName!;
}
