using Authly.Core.Enums;
using Authly.Modules.Account;
using Authly.Modules.AdvancedAuth;
using Authly.Modules.Common;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>Portal: secure change of the signed-in user's email or phone (Phase 11).</summary>
[Route("portal/contact")]
public sealed class ContactController : PortalControllerBase
{
    private readonly IContactChangeService _contactChange;
    private readonly IAccountSelfService _account;

    public ContactController(IContactChangeService contactChange, IAccountSelfService account)
    {
        _contactChange = contactChange;
        _account = account;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Email & phone";
        var user = await _account.GetAsync(TenantId, UserId, ct);
        if (user is null) return NotFound();
        ViewData["Email"] = user.Email;
        ViewData["Phone"] = user.Phone;
        return View();
    }

    [HttpPost("email")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ChangeEmail(string newEmail, CancellationToken ct)
        => StartAsync(ContactType.Email, newEmail, ct);

    [HttpPost("phone")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ChangePhone(string newPhone, CancellationToken ct)
        => StartAsync(ContactType.Phone, newPhone, ct);

    private async Task<IActionResult> StartAsync(ContactType type, string value, CancellationToken ct)
    {
        try
        {
            var outcome = await _contactChange.RequestChangeAsync(TenantId, UserId, type, value ?? "", CurrentRequest(), ct);
            TempData[outcome == ContactChangeOutcome.Started ? "Success" : "Error"] = outcome switch
            {
                ContactChangeOutcome.Started => "Check your new contact for a confirmation link. We've also alerted your current email.",
                ContactChangeOutcome.AlreadyInUse => "That email is already in use.",
                ContactChangeOutcome.Cooldown => "A change is already pending. Please wait a moment before trying again.",
                _ => "Could not start the change."
            };
        }
        catch (AdvancedAuthException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index));
    }

    private RequestInfo CurrentRequest()
        => new(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
}
