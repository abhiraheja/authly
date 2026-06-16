using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Authly.Web.Models;

namespace Authly.Web.Controllers;

public class HomeController : Controller
{
    private readonly IConfiguration _config;

    public HomeController(IConfiguration config) => _config = config;

    public IActionResult Index()
    {
        ViewBag.SuperAdminEnabled = _config.GetValue("SUPERADMIN_ENABLED", true);
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
