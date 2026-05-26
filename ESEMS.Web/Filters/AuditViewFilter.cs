using System.Security.Claims;
using Microsoft.AspNetCore.Mvc.Filters;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Filters;

/// <summary>
/// Optional read auditing for MVC GET requests.
/// Controlled by configuration key: Auditing:LogReads.
/// </summary>
public sealed class AuditViewFilter : IAsyncActionFilter
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly IConfiguration _configuration;

    public AuditViewFilter(ApplicationDbContext db, IHttpContextAccessor http, IConfiguration configuration)
    {
        _db = db;
        _http = http;
        _configuration = configuration;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (!_configuration.GetValue("Auditing:LogReads", false))
            return;

        if (executed.Exception != null && !executed.ExceptionHandled)
            return;

        var http = _http.HttpContext;
        if (http == null) return;

        if (!HttpMethods.IsGet(http.Request.Method))
            return;

        var user = http.User;
        if (user?.Identity?.IsAuthenticated != true)
            return;

        var controller = context.RouteData.Values["controller"]?.ToString();
        if (string.IsNullOrWhiteSpace(controller)) return;

        // Avoid noisy recursion and auth pages.
        if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(controller, "AuditLogs", StringComparison.OrdinalIgnoreCase))
            return;

        var action = context.RouteData.Values["action"]?.ToString();
        var id = context.RouteData.Values["id"]?.ToString();

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user.Identity?.Name;

        _db.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = AuditAction.View,
            EntityType = controller,
            EntityId = id ?? (action ?? string.Empty),
            Notes = $"{controller}/{action}",
            IpAddress = http.Connection.RemoteIpAddress?.ToString(),
            UserAgent = http.Request.Headers.UserAgent.ToString(),
            Timestamp = DateTime.UtcNow
        });

        await _db.SaveChangesAsync();
    }
}
