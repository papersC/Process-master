using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ESEMS.Web.Models;

namespace ESEMS.Web.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;

    public HomeController(ILogger<HomeController> logger)
    {
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult UserManual()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    /// <summary>
    /// Renders a friendly page for HTTP status-code re-execution
    /// (wired up via UseStatusCodePagesWithReExecute in Program.cs).
    /// Covers 404 / 403 / 500 with a human message and a route back home.
    /// </summary>
    [AllowAnonymous]
    [Route("/Home/StatusCode/{code:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCode(int code)
    {
        Response.StatusCode = code;
        ViewBag.StatusCode = code;
        return View("StatusCode");
    }
}
