using Authly.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// Serves tenant-uploaded branding images (logo / login background) by id on the public hosted
/// pages. Anonymous — the login page is unauthenticated. Assets are non-sensitive branding bytes
/// addressed by an unguessable Guid; the table has no RLS so the read works without a tenant scope.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("branding/asset")]
public sealed class BrandingAssetController : ControllerBase
{
    private readonly IBrandingAssetRepository _assets;

    public BrandingAssetController(IBrandingAssetRepository assets) => _assets = assets;

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var asset = await _assets.GetByIdAsync(id, ct);
        if (asset is null) return NotFound();

        // Immutable content (a new upload gets a new id), so cache aggressively.
        Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        return File(asset.Data, asset.ContentType);
    }
}
