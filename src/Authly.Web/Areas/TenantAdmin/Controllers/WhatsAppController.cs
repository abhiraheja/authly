using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Messaging;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Dedicated tenant-admin surface for WhatsApp: configure the MSG91 provider, link the tenant's own
/// approved named-parameter templates to Authly message keys (otp, verify_new_contact), and send
/// tests. Linking validates that a template uses only the key's allowed named variables.
/// </summary>
[Route("tenantadmin/whatsapp")]
public sealed class WhatsAppController : TenantAdminControllerBase
{
    private readonly IMessagingService _messaging;

    public WhatsAppController(IMessagingService messaging, ITenantContext tenant) : base(tenant)
        => _messaging = messaging;

    [RequireOperatorPermission("project.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "WhatsApp";

        var providers = await _messaging.ListProvidersAsync(TenantId, ct);
        var provider = providers.FirstOrDefault(p => p.Channel == MessageChannel.WhatsApp);

        var bindings = new Dictionary<string, TemplateInput>();
        foreach (var vset in WhatsAppAllowedVariables.All)
        {
            bindings[vset.Key] = await _messaging.GetTemplateForEditAsync(
                TenantId, vset.Key, MessageChannel.WhatsApp, "en", ct);
        }

        var logs = (await _messaging.ListRecentLogsAsync(TenantId, 100, ct))
            .Where(l => l.Channel == MessageChannel.WhatsApp)
            .Take(25)
            .ToList();

        return View(new WhatsAppIndexViewModel(provider, WhatsAppAllowedVariables.All, bindings, logs));
    }

    [RequireOperatorPermission("project.read")]
    [HttpGet("provider")]
    public async Task<IActionResult> Provider(Guid? id, CancellationToken ct)
    {
        ViewData["Title"] = "WhatsApp provider";
        var input = new ProviderConfigInput { Channel = MessageChannel.WhatsApp, Provider = "msg91" };
        if (id is { } pid && await _messaging.GetProviderAsync(TenantId, pid, ct) is { } existing)
        {
            input.Id = existing.Id;
            input.Provider = existing.Provider;
            input.Mode = existing.Mode;
            input.IsActive = existing.IsActive;
        }
        return View(input);
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("provider")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProvider(ProviderConfigInput input, CancellationToken ct)
    {
        input.Channel = MessageChannel.WhatsApp;
        await _messaging.SaveProviderAsync(TenantId, input, CurrentAudit(), ct);
        TempData["Success"] = "WhatsApp provider saved.";
        return RedirectToAction(nameof(Index));
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("provider/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProvider(Guid id, CancellationToken ct)
    {
        await _messaging.DeleteProviderAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "WhatsApp provider removed.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Pull the tenant's WhatsApp templates from MSG91 and show each with a per-key bind verdict.</summary>
    [RequireOperatorPermission("project.read")]
    [HttpGet("link")]
    public async Task<IActionResult> Link(CancellationToken ct)
    {
        ViewData["Title"] = "Link a WhatsApp template";
        try
        {
            var synced = await _messaging.ListSyncableWhatsAppTemplatesAsync(TenantId, ct);
            return View(synced);
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("link")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkTemplate(string key, string providerTemplateName, string providerLanguage, CancellationToken ct)
    {
        if (!WhatsAppAllowedVariables.IsSupportedKey(key)) return NotFound();
        try
        {
            await _messaging.BindWhatsAppTemplateValidatedAsync(
                TenantId, key, "en", providerTemplateName, providerLanguage, CurrentAudit(), ct);
            TempData["Success"] = $"Linked '{key}' to template '{providerTemplateName}'.";
        }
        catch (TemplateValidationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("unlink/{id:guid}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Unlink(Guid id, CancellationToken ct)
    {
        await _messaging.DeleteTemplateAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Template unlinked.";
        return RedirectToAction(nameof(Index));
    }

    [RequireOperatorPermission("project.write")]
    [HttpPost("test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(string key, string recipient, CancellationToken ct)
    {
        if (!WhatsAppAllowedVariables.IsSupportedKey(key)) return NotFound();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            TempData["Error"] = "Enter a recipient phone number for the test message.";
            return RedirectToAction(nameof(Index));
        }
        await _messaging.SendTestAsync(TenantId, key, MessageChannel.WhatsApp, recipient, ct);
        TempData["Success"] = $"Test '{key}' queued to {recipient}.";
        return RedirectToAction(nameof(Index));
    }
}

/// <summary>View model for the WhatsApp dashboard: provider status, the linkable message keys with
/// their allowed variables, the current binding (if any) per key, and recent WhatsApp deliveries.</summary>
public sealed record WhatsAppIndexViewModel(
    MessagingProvider? Provider,
    IReadOnlyList<WhatsAppVariableSet> VariableSets,
    IReadOnlyDictionary<string, TemplateInput> Bindings,
    IReadOnlyList<MessageLog> Logs);
