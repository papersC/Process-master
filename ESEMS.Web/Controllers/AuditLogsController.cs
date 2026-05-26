using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Admin-only audit log viewer.
/// </summary>
[Authorize(Policy = AppPolicies.CanAdmin)]
public class AuditLogsController : BaseController
{
    private readonly ApplicationDbContext _db;

    public AuditLogsController(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IActionResult> Index(
        DateTime? from = null,
        DateTime? to = null,
        AuditAction? action = null,
        string? entityType = null,
        string? userName = null,
        int page = 1,
        int pageSize = 50)
    {
        var query = _db.AuditLogs.AsNoTracking().AsQueryable();

        if (from.HasValue)
            query = query.Where(a => a.Timestamp >= from.Value);
        if (to.HasValue)
            query = query.Where(a => a.Timestamp <= to.Value);
        if (action.HasValue)
            query = query.Where(a => a.Action == action.Value);
        if (!string.IsNullOrWhiteSpace(entityType))
            query = query.Where(a => a.EntityType == entityType);
        if (!string.IsNullOrWhiteSpace(userName))
            query = query.Where(a => (a.UserName ?? "").Contains(userName));

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        ViewBag.CurrentPage = page;
        ViewBag.PageSize = pageSize;
        ViewBag.TotalCount = totalCount;
        ViewBag.TotalPages = (int)Math.Ceiling((double)totalCount / pageSize);
        ViewBag.FromDate = from;
        ViewBag.ToDate = to;
        ViewBag.SelectedAction = action;
        ViewBag.SelectedEntityType = entityType;
        ViewBag.SelectedUserName = userName;

        return View(items);
    }
}
