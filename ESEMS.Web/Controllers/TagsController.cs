using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Distinct-tag suggestions for the tag autocomplete (a native &lt;datalist&gt;
/// attached to every tag input). Aggregates the comma-separated Tags columns
/// across the taggable entities, splits + de-dupes them, so typing a tag offers
/// the ones already used anywhere in the system.
/// </summary>
[Authorize]
public class TagsController : Controller
{
    private readonly ApplicationDbContext _db;

    public TagsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Suggest()
    {
        var raw = new List<string?>();
        raw.AddRange(await _db.Processes.Where(x => !x.IsDeleted && x.Tags != null).Select(x => x.Tags).ToListAsync());
        raw.AddRange(await _db.Services.Where(x => !x.IsDeleted && x.Tags != null).Select(x => x.Tags).ToListAsync());
        raw.AddRange(await _db.Categories.Where(x => !x.IsDeleted && x.Tags != null).Select(x => x.Tags).ToListAsync());
        raw.AddRange(await _db.ProcessGroups.Where(x => !x.IsDeleted && x.Tags != null).Select(x => x.Tags).ToListAsync());
        raw.AddRange(await _db.Activities.Where(x => !x.IsDeleted && x.Tags != null).Select(x => x.Tags).ToListAsync());
        raw.AddRange(await _db.ProcessTasks.Where(x => !x.IsDeleted && x.Tags != null).Select(x => x.Tags).ToListAsync());
        raw.AddRange(await _db.StrategicObjectives.Where(x => !x.IsDeleted && x.Tags != null).Select(x => x.Tags).ToListAsync());

        var tags = raw
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .SelectMany(t => t!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return Json(new { tags });
    }
}
