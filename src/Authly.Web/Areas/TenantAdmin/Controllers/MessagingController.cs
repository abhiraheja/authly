using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Modules.Messaging;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// Tenant-admin configuration of messaging: BYOK providers (email + WhatsApp), template overrides
/// (with preview + send-test), and the delivery log (§4.11 / Phase 7).
/// </summary>
[Route("tenantadmin/messaging")]
public sealed class MessagingController : TenantAdminControllerBase
{
    private readonly IMessagingService _messaging;

    public MessagingController(IMessagingService messaging, ITenantContext tenant) : base(tenant)
        => _messaging = messaging;

    // --- Providers + log overview ------------------------------------------

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Messaging";
        ViewData["Providers"] = await _messaging.ListProvidersAsync(TenantId, ct);
        ViewData["Logs"] = await _messaging.ListRecentLogsAsync(TenantId, 50, ct);
        return View();
    }

    [HttpGet("providers/{channel}")]
    public async Task<IActionResult> Provider(string channel, Guid? id, CancellationToken ct)
    {
        if (!TryParseChannel(channel, out var ch)) return NotFound();
        ViewData["Title"] = $"{ch} provider";

        var input = new ProviderConfigInput { Channel = ch, Provider = ch == MessageChannel.Email ? "smtp" : "msg91" };
        if (id is { } pid && await _messaging.GetProviderAsync(TenantId, pid, ct) is { } existing)
        {
            input.Id = existing.Id;
            input.Provider = existing.Provider;
            input.Mode = existing.Mode;
            input.IsActive = existing.IsActive;
            // Secrets are write-only; non-secret fields are re-shown from the stored (decrypted) view
            // only where safe. We surface "configured" state rather than secret values.
        }
        return View(input);
    }

    [HttpPost("providers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProvider(ProviderConfigInput input, CancellationToken ct)
    {
        await _messaging.SaveProviderAsync(TenantId, input, CurrentAudit(), ct);
        TempData["Success"] = $"{input.Channel} provider saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("providers/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProvider(Guid id, CancellationToken ct)
    {
        await _messaging.DeleteProviderAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Provider removed.";
        return RedirectToAction(nameof(Index));
    }

    // --- Templates ----------------------------------------------------------

    [HttpGet("templates")]
    public async Task<IActionResult> Templates(CancellationToken ct)
    {
        ViewData["Title"] = "Message templates";
        return View(await _messaging.ListTemplatesAsync(TenantId, ct));
    }

    [HttpGet("templates/edit")]
    public async Task<IActionResult> EditTemplate(string key, string channel, string locale, CancellationToken ct)
    {
        if (!TryParseChannel(channel, out var ch)) return NotFound();
        ViewData["Title"] = $"Edit {key}";
        try
        {
            return View(await _messaging.GetTemplateForEditAsync(TenantId, key, ch, string.IsNullOrWhiteSpace(locale) ? "en" : locale, ct));
        }
        catch (TemplateNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("templates")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveTemplate(TemplateInput input, string? action, CancellationToken ct)
    {
        ViewData["Title"] = $"Edit {input.Key}";

        if (string.Equals(action, "preview", StringComparison.OrdinalIgnoreCase))
        {
            // Preview the UNSAVED body against sample variables.
            var html = input.Channel == MessageChannel.Email;
            ViewData["Preview"] = new RenderedPreview(input.Channel,
                input.Subject is null ? null : TemplateRenderer.Render(input.Subject, Sample, htmlEncode: false),
                TemplateRenderer.Render(input.Body, Sample, htmlEncode: html));
            return View(nameof(EditTemplate), input);
        }

        try
        {
            await _messaging.SaveTemplateAsync(TenantId, input, CurrentAudit(), ct);
            TempData["Success"] = "Template saved.";
            return RedirectToAction(nameof(Templates));
        }
        catch (TemplateValidationException ex)
        {
            ModelState.AddModelError(nameof(input.Body), ex.Message);
            return View(nameof(EditTemplate), input);
        }
    }

    [HttpPost("templates/{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteTemplate(Guid id, CancellationToken ct)
    {
        await _messaging.DeleteTemplateAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Template reset to the built-in default.";
        return RedirectToAction(nameof(Templates));
    }

    [HttpPost("templates/send-test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTest(string key, string channel, string recipient, CancellationToken ct)
    {
        if (!TryParseChannel(channel, out var ch)) return NotFound();
        if (string.IsNullOrWhiteSpace(recipient))
        {
            TempData["Error"] = "Enter a recipient for the test message.";
            return RedirectToAction(nameof(Templates));
        }
        await _messaging.SendTestAsync(TenantId, key, ch, recipient, ct);
        TempData["Success"] = $"Test '{key}' queued to {recipient}.";
        return RedirectToAction(nameof(Templates));
    }

    private static readonly IReadOnlyDictionary<string, string> Sample = new Dictionary<string, string>
    {
        ["app_name"] = "Authly",
        ["user_name"] = "Sample User",
        ["action_url"] = "https://example.com/verify?token=sample",
        ["otp"] = "123456",
        ["expiry_hours"] = "24",
        ["expiry_minutes"] = "10",
        ["message"] = "This is a sample security alert."
    };

    private static bool TryParseChannel(string s, out MessageChannel channel)
    {
        channel = MessageChannel.Email;
        if (string.Equals(s, "email", StringComparison.OrdinalIgnoreCase)) { channel = MessageChannel.Email; return true; }
        if (string.Equals(s, "whatsapp", StringComparison.OrdinalIgnoreCase)) { channel = MessageChannel.WhatsApp; return true; }
        return false;
    }
}
