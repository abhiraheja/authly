using Authly.Core.Common;
using Authly.Core.Interfaces;
using Authly.Modules.Authorization;
using Authly.Modules.Users;
using Authly.Web.Infrastructure.Api;
using Authly.Web.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers.Api;

/// <summary>Management API: users (§6). All actions tenant-scoped + permission-gated.</summary>
[Route("api/v1/users")]
public sealed class UsersController : ApiControllerBase
{
    private readonly IUserAdminService _users;
    private readonly IRbacService _rbac;

    public UsersController(IUserAdminService users, IRbacService rbac, ITenantContext tenant) : base(tenant)
    {
        _users = users;
        _rbac = rbac;
    }

    [HttpGet("")]
    [RequirePermission("user.read")]
    public async Task<IActionResult> List([FromQuery] int? page, [FromQuery] int? limit, [FromQuery] string? email, CancellationToken ct)
    {
        var p = Pagination.Of(page, limit);
        var result = await _users.ListAsync(TenantId, p, email, ct);
        return Ok(PagedResponse<UserResponse>.From(result, p, UserResponse.From));
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("user.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var user = await _users.GetAsync(TenantId, id, ct);
        return user is null ? NotFoundError("User not found.") : Ok(UserResponse.From(user));
    }

    [HttpPost("")]
    [RequirePermission("user.write")]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblemEnvelope();
        var user = await _users.CreateAsync(TenantId,
            new CreateUserRequest(dto.Email, dto.Password, dto.FirstName, dto.LastName, dto.EmailVerified, dto.SuppressEvents), ApiAudit(), ct);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, UserResponse.From(user));
    }

    [HttpPatch("{id:guid}")]
    [RequirePermission("user.write")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto, CancellationToken ct)
    {
        var user = await _users.UpdateAsync(TenantId, id,
            new UpdateUserRequest(dto.FirstName, dto.LastName, dto.Phone, dto.Timezone, dto.Locale), ApiAudit(), ct);
        return Ok(UserResponse.From(user));
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("user.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _users.DeleteAsync(TenantId, id, ApiAudit(), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/suspend")]
    [RequirePermission("user.write")]
    public async Task<IActionResult> Suspend(Guid id, CancellationToken ct)
    {
        await _users.SuspendAsync(TenantId, id, ApiAudit(), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/reactivate")]
    [RequirePermission("user.write")]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await _users.ReactivateAsync(TenantId, id, ApiAudit(), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/force-password-reset")]
    [RequirePermission("user.write")]
    public async Task<IActionResult> ForcePasswordReset(Guid id, CancellationToken ct)
    {
        await _users.ForcePasswordResetAsync(TenantId, id, ApiAudit(), ct);
        return Accepted();
    }

    // --- Roles ---

    [HttpGet("{id:guid}/roles")]
    [RequirePermission("user.read")]
    public async Task<IActionResult> ListRoles(Guid id, CancellationToken ct)
    {
        if (await _users.GetAsync(TenantId, id, ct) is null) return NotFoundError("User not found.");
        var roles = await _rbac.ListUserRolesAsync(TenantId, id, ct);
        return Ok(roles.Select(RoleResponse.From).ToList());
    }

    [HttpPost("{id:guid}/roles")]
    [RequirePermission("user.write")]
    public async Task<IActionResult> AssignRole(Guid id, [FromBody] AssignRoleDto dto, CancellationToken ct)
    {
        if (await _users.GetAsync(TenantId, id, ct) is null) return NotFoundError("User not found.");
        await _rbac.AssignRoleAsync(TenantId, id, dto.RoleId, ApiAudit(), ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    [RequirePermission("user.write")]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId, CancellationToken ct)
    {
        await _rbac.RemoveRoleAsync(TenantId, id, roleId, ApiAudit(), ct);
        return NoContent();
    }

    // --- Sessions ---

    [HttpGet("{id:guid}/sessions")]
    [RequirePermission("user.read")]
    public async Task<IActionResult> ListSessions(Guid id, CancellationToken ct)
    {
        var sessions = await _users.ListSessionsAsync(TenantId, id, ct);
        return Ok(sessions.Select(SessionResponse.From).ToList());
    }

    [HttpDelete("{id:guid}/sessions")]
    [RequirePermission("user.write")]
    public async Task<IActionResult> RevokeSessions(Guid id, CancellationToken ct)
    {
        var revoked = await _users.RevokeAllSessionsAsync(TenantId, id, ApiAudit(), ct);
        return Ok(new { revoked });
    }

    private IActionResult ValidationProblemEnvelope()
    {
        var first = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault()
                    ?? "Invalid request.";
        return ApiError(StatusCodes.Status400BadRequest, "validation_error", first);
    }
}
