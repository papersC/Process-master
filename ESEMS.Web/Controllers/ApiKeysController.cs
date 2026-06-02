using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Admin helper for the OUTBOUND read API (<c>api/v1/*</c>). Generates a strong
/// random API key that an administrator pastes into configuration
/// (<c>OutboundApi:ApiKeys</c>) or user-secrets; the key is then accepted by
/// <see cref="ApiKeyRequiredAttribute"/>. Cookie-authenticated and admin-only —
/// the outbound API itself uses X-Api-Key, but generating/managing keys is an
/// in-app administrative action.
/// </summary>
[Authorize(Policy = AppPolicies.CanAdmin)]
public sealed class ApiKeysController : Controller
{
    /// <summary>
    /// Returns a freshly generated, URL-safe 256-bit API key. Not persisted —
    /// the admin copies it into OutboundApi:ApiKeys. Shown once.
    /// </summary>
    [HttpGet]
    [Route("ApiKeys/Generate")]
    public IActionResult Generate()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var key = "esems_" + Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        return Json(new
        {
            apiKey = key,
            usage = "Send as header 'X-Api-Key' to /api/v1/* endpoints.",
            note = "Add this value to OutboundApi:ApiKeys in configuration / user-secrets. Shown once — store it securely."
        });
    }
}
