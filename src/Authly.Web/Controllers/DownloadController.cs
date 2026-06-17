using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Authly.Web.Controllers;

/// <summary>
/// Serves the self-host artifacts (the self-contained Compose file and its .env template) as
/// file downloads with the right content type and filename — so a new operator can grab the
/// stack without cloning the repo. The files live under wwwroot/downloads/.
/// </summary>
[AllowAnonymous]
[Route("download")]
public sealed class DownloadController : Controller
{
    private readonly IWebHostEnvironment _env;

    public DownloadController(IWebHostEnvironment env) => _env = env;

    [HttpGet("docker-compose")]
    public IActionResult DockerCompose() =>
        Serve("docker-compose.yml", "application/yaml", "docker-compose.yml");

    [HttpGet("env-example")]
    public IActionResult EnvExample() =>
        Serve(".env.example", "text/plain", ".env.example");

    private IActionResult Serve(string fileName, string contentType, string downloadName)
    {
        var path = Path.Combine(_env.WebRootPath, "downloads", fileName);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, contentType, downloadName);
    }
}
