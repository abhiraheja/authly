using Authly.Core.WebAuthn;
using Authly.Modules.AdvancedAuth;
using Authly.Web.Infrastructure.WebAuthn;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.Portal.Controllers;

/// <summary>Portal: enrol and manage passkeys (WebAuthn) for the signed-in user (Phase 11).</summary>
[Route("portal/passkeys")]
public sealed class PasskeysController : PortalControllerBase
{
    private const string Purpose = "register";

    private readonly IPasskeyService _passkeys;
    private readonly WebAuthnChallengeStore _store;

    public PasskeysController(IPasskeyService passkeys, WebAuthnChallengeStore store)
    {
        _passkeys = passkeys;
        _store = store;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Passkeys";
        return View(await _passkeys.ListAsync(TenantId, UserId, ct));
    }

    [HttpPost("begin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Begin(CancellationToken ct)
    {
        var challenge = await _passkeys.BeginRegistrationAsync(TenantId, UserId, ct);
        _store.Save(HttpContext, new WebAuthnPending(Purpose, TenantId, UserId, challenge.State));
        return Content(challenge.OptionsJson, "application/json");
    }

    [HttpPost("complete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete([FromForm] string response, [FromForm] string? friendlyName, CancellationToken ct)
    {
        var pending = _store.Read(HttpContext);
        if (pending is null || pending.Purpose != Purpose || pending.TenantId != TenantId || pending.UserId != UserId)
            return BadRequest(new { error = "expired" });
        _store.Clear(HttpContext);

        try
        {
            await _passkeys.CompleteRegistrationAsync(TenantId, UserId, pending.State, response, friendlyName, CurrentAudit(), ct);
            return Ok(new { redirect = Url.Action(nameof(Index)) });
        }
        catch (WebAuthnException)
        {
            return BadRequest(new { error = "verification_failed" });
        }
    }

    [HttpPost("{id:guid}/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id, CancellationToken ct)
    {
        await _passkeys.RemoveAsync(TenantId, UserId, id, CurrentAudit(), ct);
        TempData["Success"] = "Passkey removed.";
        return RedirectToAction(nameof(Index));
    }
}
