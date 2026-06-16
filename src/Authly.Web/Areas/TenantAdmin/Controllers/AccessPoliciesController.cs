using System.Text.Json;
using Authly.Core.Interfaces;
using Authly.Modules.Abac;
using Authly.Web.Areas.TenantAdmin.Models;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin CRUD for ABAC access policies, plus a decision test console.</summary>
[Route("tenantadmin/policies")]
public sealed class AccessPoliciesController : TenantAdminControllerBase
{
    private readonly IAccessPolicyService _policies;
    private readonly IAuthorizationDecisionService _decisions;

    public AccessPoliciesController(IAccessPolicyService policies, IAuthorizationDecisionService decisions,
        ITenantContext tenant) : base(tenant)
    {
        _policies = policies;
        _decisions = decisions;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Access policies";
        return View(await _policies.ListAsync(TenantId, ct));
    }

    [HttpGet("create")]
    public IActionResult Create() => View("Edit", new AccessPolicyViewModel());

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Edit(Guid id, CancellationToken ct)
    {
        var p = await _policies.GetAsync(TenantId, id, ct);
        if (p is null) return NotFound();
        return View(new AccessPolicyViewModel
        {
            Id = p.Id, Name = p.Name, Description = p.Description,
            Effect = string.Equals(p.Effect, "deny", StringComparison.OrdinalIgnoreCase) ? PolicyEffect.Deny : PolicyEffect.Allow,
            Action = p.Action, ResourceType = p.ResourceType, ConditionsJson = p.Conditions,
            Priority = p.Priority, Enabled = p.Enabled
        });
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AccessPolicyViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid) return View("Edit", model);
        try
        {
            var conditions = AbacJson.ParseConditionsStrict(model.ConditionsJson);
            var input = new AccessPolicyInput(model.Name, model.Description, model.Effect,
                model.Action, model.ResourceType, conditions, model.Priority, model.Enabled);

            if (model.Id is { } id)
                await _policies.UpdateAsync(TenantId, id, input, CurrentAudit(), ct);
            else
                await _policies.CreateAsync(TenantId, input, CurrentAudit(), ct);

            TempData["Success"] = "Policy saved.";
            return RedirectToAction(nameof(Index));
        }
        catch (AccessPolicyInvalidException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Edit", model);
        }
    }

    [HttpPost("{id:guid}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        try
        {
            await _policies.DeleteAsync(TenantId, id, CurrentAudit(), ct);
            TempData["Success"] = "Policy deleted.";
        }
        catch (KeyNotFoundException) { TempData["Error"] = "That policy no longer exists."; }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("test")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(AccessPolicyTestViewModel test, CancellationToken ct)
    {
        ViewData["Title"] = "Access policies";
        try
        {
            var request = new AccessRequest(
                (test.Action ?? "").Trim(), (test.ResourceType ?? "").Trim(),
                ParseDict(test.SubjectJson), ParseDict(test.ResourceJson), ParseDict(test.EnvironmentJson));
            ViewBag.TestResult = await _decisions.EvaluateAsync(TenantId, request, ct);
        }
        catch (JsonException)
        {
            TempData["Error"] = "Attribute JSON must be an object of string values, e.g. {\"department\":\"eng\"}.";
        }
        ViewBag.Test = test;
        return View(nameof(Index), await _policies.ListAsync(TenantId, ct));
    }

    private static Dictionary<string, string> ParseDict(string? json)
        => string.IsNullOrWhiteSpace(json)
            ? new()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
}
