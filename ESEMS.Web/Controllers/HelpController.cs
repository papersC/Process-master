using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Help hub — landing page for user guides, quick starts, FAQs, and the
/// downloadable user manual. Inspired by the PManagement Help section but
/// styled with the ESEMS brand palette.
/// </summary>
[Authorize]
public class HelpController : BaseController
{
    public IActionResult Index() => View();
}
