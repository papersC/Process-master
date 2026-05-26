using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Serves the "My Space" page — a per-user document library. The page
/// itself is static; all data is loaded by the matching
/// <see cref="Api.MySpaceController"/> API.
/// </summary>
[Authorize]
public class MySpaceController : BaseController
{
    public IActionResult Index() => View();
}
