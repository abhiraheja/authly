using Authly.Modules.Announcements;
using Authly.Web.Areas.SuperAdmin.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.SuperAdmin.Controllers;

/// <summary>Super-admin CRUD for platform announcements shown to tenant admins.</summary>
public sealed class AnnouncementsController : SuperAdminControllerBase
{
    private readonly IAnnouncementService _announcements;

    public AnnouncementsController(IAnnouncementService announcements) => _announcements = announcements;

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _announcements.ListAllAsync(ct));

    [HttpGet]
    public IActionResult Create() => View("Edit", new AnnouncementViewModel());

    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var a = await _announcements.GetAsync(id, ct);
        if (a is null) return NotFound();
        return View(new AnnouncementViewModel
        {
            Id = a.Id, Title = a.Title, Body = a.Body, Severity = a.Severity,
            IsActive = a.IsActive, ExpiresAt = a.ExpiresAt
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AnnouncementViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", model);

        var input = new AnnouncementInput(model.Title, model.Body, model.Severity, model.IsActive, model.ExpiresAt);
        if (model.Id is { } id)
            await _announcements.UpdateAsync(id, input, CurrentAudit(), ct);
        else
            await _announcements.CreateAsync(input, CurrentAudit(), ct);

        TempData["Success"] = "Announcement saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _announcements.DeleteAsync(id, CurrentAudit(), ct);
        TempData["Success"] = "Announcement deleted.";
        return RedirectToAction(nameof(Index));
    }
}
