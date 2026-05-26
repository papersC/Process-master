using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Localization;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Request model for changing language
/// </summary>
public class ChangeLanguageRequest
{
    public string Culture { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
}

/// <summary>
/// Base controller with common functionality
/// </summary>
public class BaseController : Controller
{
    private IStringLocalizer<SharedResource>? _localizer;

    protected IStringLocalizer<SharedResource> Localizer =>
        _localizer ??= HttpContext.RequestServices.GetRequiredService<IStringLocalizer<SharedResource>>();

    /// <summary>
    /// Changes the application language
    /// </summary>
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public IActionResult ChangeLanguage([FromBody] ChangeLanguageRequest model)
    {
        if (string.IsNullOrEmpty(model.Culture))
        {
            return BadRequest(Localizer["Error_CultureRequired"].Value);
        }

        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(model.Culture)),
            new CookieOptions
            {
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            }
        );

        return Json(new { success = true, redirectUrl = string.IsNullOrEmpty(model.ReturnUrl) ? "/" : model.ReturnUrl });
    }

    /// <summary>
    /// Gets the current culture
    /// </summary>
    protected string CurrentCulture => System.Globalization.CultureInfo.CurrentUICulture.Name;

    /// <summary>
    /// Checks if current culture is Arabic
    /// </summary>
    protected bool IsArabic => CurrentCulture.StartsWith("ar");
}

