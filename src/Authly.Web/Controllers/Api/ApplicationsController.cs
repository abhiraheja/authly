using Authly.Core.Interfaces;
using Authly.Modules.Applications;
using Authly.Web.Infrastructure.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers.Api;

/// <summary>Management API: OAuth applications + secret rotation (§6).</summary>
[Route("api/v1/applications")]
public sealed class ApplicationsController : ApiControllerBase
{
    private readonly IApplicationService _apps;

    public ApplicationsController(IApplicationService apps, ITenantContext tenant) : base(tenant) => _apps = apps;

    [HttpGet("")]
    [RequirePermission("application.read")]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var apps = await _apps.ListAsync(TenantId, ct);
        return Ok(apps.Select(ApplicationResponse.From).ToList());
    }

    [HttpGet("{id:guid}")]
    [RequirePermission("application.read")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var app = await _apps.GetAsync(TenantId, id, ct);
        return app is null ? NotFoundError("Application not found.") : Ok(ApplicationResponse.From(app));
    }

    [HttpPost("")]
    [RequirePermission("application.write")]
    public async Task<IActionResult> Create([FromBody] CreateApplicationDto dto, CancellationToken ct)
    {
        var result = await _apps.CreateAsync(TenantId,
            new CreateApplicationRequest(dto.Name, dto.Type, dto.RedirectUris, dto.Scopes, dto.PostLogoutRedirectUris),
            ApiAudit(), ct);

        // The client secret is returned once here and never again.
        return CreatedAtAction(nameof(Get), new { id = result.Application.Id }, new
        {
            application = ApplicationResponse.From(result.Application),
            clientSecret = result.ClientSecret
        });
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission("application.delete")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _apps.DeleteAsync(TenantId, id, ApiAudit(), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/secrets/rotate")]
    [RequirePermission("application.write")]
    public async Task<IActionResult> RotateSecret(Guid id, CancellationToken ct)
    {
        var secret = await _apps.RotateSecretAsync(TenantId, id, ApiAudit(), ct);
        return Ok(new { clientSecret = secret });
    }
}
