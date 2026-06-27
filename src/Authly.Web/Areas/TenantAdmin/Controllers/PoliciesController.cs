using Authly.Core.Entities;
using Authly.Core.Enums;
using Authly.Core.Interfaces;
using Authly.Core.Policies;
using Authly.Modules.Policies;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>
/// TenantAdmin management of policies (Terms &amp; Conditions / norms): author a draft (HTML or PDF),
/// publish it with an enforcement mode + targeting, review who has accepted/skipped/declined, and
/// re-request acceptance. Gated on <c>policy.read</c> / <c>policy.manage</c>.
/// </summary>
[Route("tenantadmin/consent-policies")]
public sealed class PoliciesController : TenantAdminControllerBase
{
    /// <summary>Auth-method targeting options shown in the editor.</summary>
    private static readonly string[] AuthMethodOptions =
        { AuthMethodCategories.Password, AuthMethodCategories.Social, AuthMethodCategories.Passkey, AuthMethodCategories.Phone, AuthMethodCategories.MagicLink };

    private readonly IPolicyService _policies;
    private readonly IApplicationRepository _applications;
    private readonly ISocialProviderRepository _socialProviders;

    public PoliciesController(IPolicyService policies, IApplicationRepository applications,
        ISocialProviderRepository socialProviders, ITenantContext tenant) : base(tenant)
    {
        _policies = policies;
        _applications = applications;
        _socialProviders = socialProviders;
    }

    [RequireOperatorPermission("policy.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Policies";
        var policies = await _policies.ListAsync(TenantId, ct);
        return View(policies);
    }

    [RequireOperatorPermission("policy.manage")]
    [HttpGet("edit/{id:guid?}")]
    public async Task<IActionResult> Edit(Guid? id, CancellationToken ct)
    {
        ViewData["Title"] = id is null ? "New policy" : "Edit policy";
        var vm = new PolicyEditViewModel();

        if (id is { } pid)
        {
            var policy = await _policies.GetAsync(TenantId, pid, ct);
            if (policy is null) return NotFound();
            var t = PolicyTargetingJson.Parse(policy.Targeting);
            vm = new PolicyEditViewModel
            {
                Id = policy.Id,
                Title = policy.Title,
                Description = policy.Description,
                EnforcementMode = policy.EnforcementMode.ToString(),
                SkipDeadline = policy.SkipDeadline?.UtcDateTime,
                StartsAt = policy.StartsAt?.UtcDateTime,
                CloseDate = policy.CloseDate?.UtcDateTime,
                ContentType = policy.DraftContentType.ToString(),
                HtmlContent = policy.DraftHtmlContent,
                Audience = t.Audience,
                ApplicationIds = t.ApplicationIds,
                AuthMethods = t.AuthMethods,
                Providers = t.Providers,
                Status = policy.Status.ToString(),
                HasDraftPdf = policy.DraftContentType == PolicyContentType.Pdf && policy.DraftAssetId is not null,
                DraftPdfAssetId = policy.DraftAssetId?.ToString(),
            };
        }

        await PopulateOptionsAsync(vm, ct);
        return View(vm);
    }

    [RequireOperatorPermission("policy.manage")]
    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PolicyEditViewModel vm, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(vm, ct);
            return View(nameof(Edit), vm);
        }

        var input = ToInput(vm);
        if (vm.Id is { } id)
        {
            await _policies.UpdateAsync(TenantId, id, input, CurrentAudit(), ct);
            TempData["Success"] = "Policy saved.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        var created = await _policies.CreateAsync(TenantId, input, CurrentAudit(), ct);
        TempData["Success"] = "Draft created. Add content and publish when ready.";
        return RedirectToAction(nameof(Edit), new { id = created.Id });
    }

    [RequireOperatorPermission("policy.manage")]
    [HttpPost("{id:guid}/pdf")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> UploadPdf(Guid id, IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            TempData["Error"] = "Choose a PDF to upload.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        try
        {
            await _policies.UploadPdfAsync(TenantId, id, ms.ToArray(), file.ContentType, file.FileName, CurrentAudit(), ct);
            TempData["Success"] = "PDF uploaded as the draft content.";
        }
        catch (PolicyInvalidException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    [RequireOperatorPermission("policy.manage")]
    [HttpPost("{id:guid}/publish")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id, CancellationToken ct)
    {
        try
        {
            await _policies.PublishAsync(TenantId, id, CurrentAudit(), ct);
            TempData["Success"] = "Policy published. Targeted users will be prompted on next sign-in.";
        }
        catch (PolicyInvalidException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Edit), new { id });
    }

    [RequireOperatorPermission("policy.manage")]
    [HttpPost("{id:guid}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Archive(Guid id, CancellationToken ct)
    {
        await _policies.ArchiveAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Policy archived — it will no longer be shown.";
        return RedirectToAction(nameof(Index));
    }

    [RequireOperatorPermission("policy.manage")]
    [HttpPost("{id:guid}/re-request")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReRequest(Guid id, CancellationToken ct)
    {
        await _policies.ReRequestAcceptanceAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "Acceptance re-requested — everyone will be prompted again on next sign-in.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [RequireOperatorPermission("policy.read")]
    [HttpGet("{id:guid}/responses")]
    public async Task<IActionResult> Responses(Guid id, CancellationToken ct)
    {
        var policy = await _policies.GetAsync(TenantId, id, ct);
        if (policy is null) return NotFound();
        ViewData["Title"] = "Policy responses";

        var decisions = await _policies.ListDecisionsAsync(TenantId, id, ct);
        // Latest decision per user reflects their current standing.
        var latestPerUser = decisions
            .GroupBy(d => d.UserId)
            .Select(g => g.OrderByDescending(d => d.DecidedAt).First())
            .ToList();

        return View(new PolicyResponsesViewModel
        {
            Policy = policy,
            Accepted = latestPerUser.Count(d => d.Decision == PolicyDecisionType.Accepted),
            Rejected = latestPerUser.Count(d => d.Decision == PolicyDecisionType.Rejected),
            Skipped = latestPerUser.Count(d => d.Decision == PolicyDecisionType.Skipped),
            Recent = decisions.Take(100).ToList()
        });
    }

    // --- helpers ---

    private PolicyEditInput ToInput(PolicyEditViewModel vm)
    {
        Enum.TryParse<PolicyEnforcementMode>(vm.EnforcementMode, out var mode);
        Enum.TryParse<PolicyContentType>(vm.ContentType, out var contentType);

        var targeting = new PolicyTargeting
        {
            Audience = vm.Audience switch
            {
                Audiences.Applications or Audiences.AuthMethods or Audiences.Providers => vm.Audience,
                _ => Audiences.All
            },
            ApplicationIds = vm.ApplicationIds ?? new(),
            AuthMethods = vm.AuthMethods ?? new(),
            Providers = vm.Providers ?? new()
        };

        return new PolicyEditInput
        {
            Title = vm.Title,
            Description = vm.Description,
            EnforcementMode = mode,
            SkipDeadline = ToUtc(vm.SkipDeadline),
            StartsAt = ToUtc(vm.StartsAt),
            CloseDate = ToUtc(vm.CloseDate),
            ContentType = contentType,
            HtmlContent = vm.HtmlContent,
            Targeting = targeting
        };
    }

    private static DateTimeOffset? ToUtc(DateTime? dt)
        => dt is { } d ? new DateTimeOffset(DateTime.SpecifyKind(d, DateTimeKind.Utc)) : null;

    private async Task PopulateOptionsAsync(PolicyEditViewModel vm, CancellationToken ct)
    {
        var apps = await _applications.ListByTenantAsync(TenantId, ct);
        vm.AvailableApps = apps.Select(a => new PolicyEditViewModel.AppOption(a.Id, a.Name)).ToList();
        var providers = await _socialProviders.ListByTenantAsync(TenantId, ct);
        vm.AvailableProviders = providers.Select(p => p.Provider).Distinct().ToList();
        ViewData["AuthMethodOptions"] = AuthMethodOptions;
    }
}
