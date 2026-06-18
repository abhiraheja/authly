using System.Text.Json;
using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Modules.Common;
using Authly.Modules.Mfa;
using Authly.Modules.Users;
using Authly.Web.Areas.TenantAdmin.Models;
using Authly.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Areas.TenantAdmin.Controllers;

/// <summary>Tenant-admin view of users and their role assignments.</summary>
[Route("tenantadmin/users")]
public sealed class UsersController : TenantAdminControllerBase
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly IUserRepository _users;
    private readonly IRbacService _rbac;
    private readonly IImpersonationService _impersonation;
    private readonly IUserImportService _import;
    private readonly IUserAdminService _admin;
    private readonly IMfaService _mfa;

    public UsersController(IUserRepository users, IRbacService rbac, IImpersonationService impersonation,
        IUserImportService import, IUserAdminService admin, IMfaService mfa, ITenantContext tenant) : base(tenant)
    {
        _users = users;
        _rbac = rbac;
        _impersonation = impersonation;
        _import = import;
        _admin = admin;
        _mfa = mfa;
    }

    [RequireOperatorPermission("enduser.read")]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Users";
        return View(await _users.ListByTenantAsync(TenantId, ct));
    }

    [RequireOperatorPermission("enduser.read")]
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Details(Guid id, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(TenantId, id, ct);
        if (user is null) return NotFound();

        var assigned = await _rbac.ListUserRolesAsync(TenantId, id, ct);
        var all = await _rbac.ListRolesAsync(TenantId, ct);
        var assignedIds = assigned.Select(r => r.Id).ToHashSet();

        var factors = await _mfa.ListFactorsAsync(TenantId, id, ct);
        var backupCodes = await _mfa.CountUnusedBackupCodesAsync(id, ct);
        var sessions = await _admin.ListSessionsAsync(TenantId, id, ct);

        ViewData["Title"] = user.Email;
        return View(new UserDetailViewModel
        {
            User = user,
            AssignedRoles = assigned,
            AvailableRoles = all.Where(r => !assignedIds.Contains(r.Id)).ToList(),
            Factors = factors,
            UnusedBackupCodes = backupCodes,
            Sessions = sessions,
            UserMetadataJson = PrettyJson(user.UserMetadata),
            AppMetadataJson = PrettyJson(user.AppMetadata),
            RawJson = JsonSerializer.Serialize(new
            {
                user.Id,
                user.TenantId,
                user.Email,
                user.EmailVerified,
                user.Username,
                user.Phone,
                user.PhoneVerified,
                HasPassword = user.PasswordHash is not null,
                Status = user.Status.ToString(),
                user.IsAnonymous,
                user.FirstName,
                user.LastName,
                user.AvatarUrl,
                user.Timezone,
                user.Locale,
                user.CreatedAt,
                user.UpdatedAt,
                user.LastLoginAt
            }, JsonOpts)
        });
    }

    /// <summary>Update a user's profile fields (name, phone, timezone, locale).</summary>
    [RequireOperatorPermission("enduser.manage")]
    [HttpPost("{id:guid}/profile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile(Guid id, EditUserProfileViewModel form, CancellationToken ct)
    {
        try
        {
            await _admin.UpdateAsync(TenantId, id,
                new UpdateUserRequest(form.FirstName, form.LastName, form.Phone, form.Timezone, form.Locale),
                CurrentAudit(), ct);
            TempData["Success"] = "Profile updated.";
        }
        catch (UserNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Suspend a user (revokes their sessions) so they can no longer sign in.</summary>
    [RequireOperatorPermission("enduser.manage")]
    [HttpPost("{id:guid}/suspend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        await _admin.SuspendAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "User suspended.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [RequireOperatorPermission("enduser.manage")]
    [HttpPost("{id:guid}/reactivate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await _admin.ReactivateAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = "User reactivated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Revoke all sessions and email a password-reset link so the user must set a new password.</summary>
    [RequireOperatorPermission("enduser.manage")]
    [HttpPost("{id:guid}/force-password-reset")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForcePasswordReset(Guid id, CancellationToken ct)
    {
        try
        {
            await _admin.ForcePasswordResetAsync(TenantId, id, CurrentAudit(), ct);
            TempData["Success"] = "Sessions revoked and a password-reset email has been sent.";
        }
        catch (UserNotFoundException)
        {
            return NotFound();
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Sign the user out of every active session.</summary>
    [RequireOperatorPermission("enduser.manage")]
    [HttpPost("{id:guid}/sessions/revoke-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeSessions(Guid id, CancellationToken ct)
    {
        var revoked = await _admin.RevokeAllSessionsAsync(TenantId, id, CurrentAudit(), ct);
        TempData["Success"] = $"Revoked {revoked} session(s).";
        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>Remove an MFA factor — the recovery path when a user is locked out of their authenticator.</summary>
    [RequireOperatorPermission("enduser.manage")]
    [HttpPost("{id:guid}/factors/{factorId:guid}/disable")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableFactor(Guid id, Guid factorId, CancellationToken ct)
    {
        await _mfa.DisableFactorAsync(TenantId, id, factorId, CurrentAudit(), ct);
        TempData["Success"] = "Two-step factor removed.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private static string PrettyJson(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "{}";
        try
        {
            using var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc.RootElement, JsonOpts);
        }
        catch (JsonException)
        {
            return raw; // surface as-is if it isn't valid JSON
        }
    }

    [RequireOperatorPermission("enduser.manage")]
    [HttpPost("{id:guid}/roles/assign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(Guid id, Guid roleId, CancellationToken ct)
    {
        try
        {
            await _rbac.AssignRoleAsync(TenantId, id, roleId, CurrentAudit(), ct);
            TempData["Success"] = "Role assigned.";
        }
        catch (RoleNotFoundException)
        {
            TempData["Error"] = "That role no longer exists.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [RequireOperatorPermission("enduser.manage")]
    [HttpPost("{id:guid}/roles/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(Guid id, Guid roleId, CancellationToken ct)
    {
        try
        {
            await _rbac.RemoveRoleAsync(TenantId, id, roleId, CurrentAudit(), ct);
            TempData["Success"] = "Role removed.";
        }
        catch (RoleNotFoundException)
        {
            TempData["Error"] = "That role no longer exists.";
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /// <summary>
    /// Start impersonating a user: mint a session for them and issue the end-user cookie carrying
    /// the impersonator's identity (so the portal shows a banner and the act is reversible). The
    /// admin's own TenantAdmin cookie is untouched, so "stop" returns them straight here.
    /// </summary>
    [RequireOperatorPermission("enduser.manage")]
    [HttpPost("{id:guid}/impersonate")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Impersonate(Guid id, CancellationToken ct)
    {
        var info = new RequestInfo(
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null);
        try
        {
            var result = await _impersonation.StartAsync(TenantId, CurrentUserId, id, info, CurrentAudit(), ct);
            await UserSignIn.SignInAsync(HttpContext, result.User.Id, result.User.Email, result.User.TenantId,
                result.Session.Id, result.User.EmailVerified,
                impersonatorId: CurrentUserId, impersonatorEmail: User.Identity?.Name);
            return Redirect("/portal/profile");
        }
        catch (ImpersonationNotAllowedException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Details), new { id });
        }
    }

    [RequireOperatorPermission("enduser.read")]
    [HttpGet("import")]
    public IActionResult Import()
    {
        ViewData["Title"] = "Import users";
        return View();
    }

    [RequireOperatorPermission("enduser.manage")]
    [HttpPost("import")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(ImportSource source, string? json, CancellationToken ct)
    {
        ViewData["Title"] = "Import users";
        if (string.IsNullOrWhiteSpace(json))
        {
            TempData["Error"] = "Paste the export JSON to import.";
            return View();
        }

        var result = await _import.ImportAsync(TenantId, source, json, CurrentAudit(), ct);
        ViewBag.Result = result;
        TempData["Success"] = $"Imported {result.Created} user(s); {result.Skipped} skipped.";
        return View();
    }
}
