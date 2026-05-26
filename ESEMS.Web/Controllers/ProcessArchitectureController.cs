using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

// Friendly alias for the intuitive deep-link /ProcessArchitecture/Overview.
// The canonical landing page lives on ProcessesController.Dashboard; this
// controller exists so users who type or bookmark the section name don't 404.
// Audit H18 (2026-05-19 QA): the controller was missing class-level
// [Authorize]. In practice not a bypass (the redirect target requires
// Process.View) but adding the attribute matches the convention used by
// every other controller and short-circuits unauthenticated probes.
[Authorize(Policy = AppPolicies.Module.Process.View)]
public class ProcessArchitectureController : BaseController
{
    public IActionResult Overview()
        => RedirectToActionPermanent("Dashboard", "Processes");
}
