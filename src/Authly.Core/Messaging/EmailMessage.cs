namespace Authly.Core.Messaging;

/// <summary>
/// A transactional email to be delivered. Built by module services and handed to the
/// queue; the configured <see cref="Interfaces.IEmailSender"/> performs delivery out of
/// band (via Hangfire). Phase 2 uses a stub sender; a real provider replaces it later.
/// </summary>
/// <param name="ToEmail">Recipient address.</param>
/// <param name="ToName">Optional recipient display name.</param>
/// <param name="Subject">Subject line.</param>
/// <param name="HtmlBody">HTML body.</param>
/// <param name="TextBody">Plain-text fallback body.</param>
/// <param name="TenantId">Tenant the message belongs to (for BYOK provider selection later).</param>
public sealed record EmailMessage(
    string ToEmail,
    string? ToName,
    string Subject,
    string HtmlBody,
    string TextBody,
    Guid? TenantId = null);
