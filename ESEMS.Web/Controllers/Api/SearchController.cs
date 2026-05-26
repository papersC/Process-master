using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Extensions;
using ESEMS.Web.Services.Common;

namespace ESEMS.Web.Controllers.Api
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class SearchController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IScopingService _scopingService;

        public SearchController(ApplicationDbContext context, IScopingService scopingService)
        {
            _context = context;
            _scopingService = scopingService;
        }

        /// <summary>
        /// Global search endpoint for Quick Search (Ctrl+K)
        /// </summary>
        [HttpGet("/api/search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
            {
                return Ok(new { results = Array.Empty<object>() });
            }

            var query = q.ToLower();
            var results = new List<object>();
            // F-005: return names/descriptions in the caller's UI culture (was
            // English-only on Arabic pages). F-016: prefix result URLs with the
            // request PathBase so links resolve under sub-app hosting (e.g. /App).
            var isAr = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
            var basePath = Request.PathBase.HasValue ? Request.PathBase.Value : "";

            // SEC-004: global search must respect the caller's data-visibility
            // scope. Resolve it once, then filter every entity query by the
            // same scope extension the corresponding Index action uses. Admin /
            // Unscoped users (ScopeLevel=All) get the no-op overloads and still
            // see everything.
            var scope = await _scopingService.GetScopeAsync(User);

            // Search Processes (IOwnedByUnit)
            var processes = await _context.Processes
                .Where(p => p.NameEn.ToLower().Contains(query) ||
                           p.NameAr.Contains(query) ||
                           (p.DescriptionEn != null && p.DescriptionEn.ToLower().Contains(query)))
                .ApplyOwningUnitScope(scope)
                .Take(5)
                .Select(p => new {
                    type = "Process",
                    name = isAr ? p.NameAr : p.NameEn,
                    description = isAr ? (p.DescriptionAr ?? "") : (p.DescriptionEn ?? ""),
                    url = basePath + "/Processes/Details/" + p.Id
                })
                .ToListAsync();
            results.AddRange(processes);

            // Search Services (IOwnedByUnit)
            var services = await _context.Services
                .Where(s => s.NameEn.ToLower().Contains(query) ||
                           s.NameAr.Contains(query) ||
                           (s.DescriptionEn != null && s.DescriptionEn.ToLower().Contains(query)))
                .ApplyOwningUnitScope(scope)
                .Take(5)
                .Select(s => new {
                    type = "Service",
                    name = isAr ? s.NameAr : s.NameEn,
                    description = isAr ? (s.DescriptionAr ?? "") : (s.DescriptionEn ?? ""),
                    url = basePath + "/Services/Details/" + s.Id
                })
                .ToListAsync();
            results.AddRange(services);

            // Search Enterprise Risks (IOrganizationScoped)
            var risks = await _context.EnterpriseRisks
                .Where(r => r.NameEn.ToLower().Contains(query) ||
                           r.NameAr.Contains(query) ||
                           (r.DescriptionEn != null && r.DescriptionEn.ToLower().Contains(query)))
                .ApplyOrganizationScope(scope)
                .Take(5)
                .Select(r => new {
                    type = "Risk",
                    name = isAr ? r.NameAr : r.NameEn,
                    description = isAr ? (r.DescriptionAr ?? "") : (r.DescriptionEn ?? ""),
                    url = basePath + "/EnterpriseRisks/Details/" + r.Id
                })
                .ToListAsync();
            results.AddRange(risks);

            // Search Incidents (IAssignedToUnit)
            var incidents = await _context.Incidents
                .Where(i => i.NameEn.ToLower().Contains(query) ||
                           i.NameAr.Contains(query) ||
                           (i.DescriptionEn != null && i.DescriptionEn.ToLower().Contains(query)))
                .ApplyAssignedUnitScope(scope)
                .Take(5)
                .Select(i => new {
                    type = "Incident",
                    name = isAr ? i.NameAr : i.NameEn,
                    description = isAr ? (i.DescriptionAr ?? "") : (i.DescriptionEn ?? ""),
                    url = basePath + "/Incidents/Details/" + i.Id
                })
                .ToListAsync();
            results.AddRange(incidents);

            // Search Problems (IAssignedToUnit)
            var problems = await _context.Problems
                .Where(p => p.NameEn.ToLower().Contains(query) ||
                           p.NameAr.Contains(query) ||
                           (p.DescriptionEn != null && p.DescriptionEn.ToLower().Contains(query)))
                .ApplyAssignedUnitScope(scope)
                .Take(5)
                .Select(p => new {
                    type = "Problem",
                    name = isAr ? p.NameAr : p.NameEn,
                    description = isAr ? (p.DescriptionAr ?? "") : (p.DescriptionEn ?? ""),
                    url = basePath + "/Problems/Details/" + p.Id
                })
                .ToListAsync();
            results.AddRange(problems);

            // Search Improvements (IOwnedByUnit; ImprovementInitiatives has TitleEn)
            var improvements = await _context.ImprovementInitiatives
                .Where(i => i.TitleEn.ToLower().Contains(query) ||
                           i.TitleAr.Contains(query) ||
                           (i.DescriptionEn != null && i.DescriptionEn.ToLower().Contains(query)))
                .ApplyOwningUnitScope(scope)
                .Take(5)
                .Select(i => new {
                    type = "Improvement",
                    name = isAr ? i.TitleAr : i.TitleEn,
                    description = isAr ? (i.DescriptionAr ?? "") : (i.DescriptionEn ?? ""),
                    url = basePath + "/Improvements/Details/" + i.Id
                })
                .ToListAsync();
            results.AddRange(improvements);

            // Search Assets (IAssignedToUnit)
            var assets = await _context.Assets
                .Where(a => a.NameEn.ToLower().Contains(query) ||
                           a.NameAr.Contains(query) ||
                           (a.DescriptionEn != null && a.DescriptionEn.ToLower().Contains(query)))
                .ApplyAssignedUnitScope(scope)
                .Take(5)
                .Select(a => new {
                    type = "Asset",
                    name = isAr ? a.NameAr : a.NameEn,
                    description = isAr ? (a.DescriptionAr ?? "") : (a.DescriptionEn ?? ""),
                    url = basePath + "/Assets/Details/" + a.Id
                })
                .ToListAsync();
            results.AddRange(assets);

            // Limit total results to 20
            return Ok(new { results = results.Take(20) });
        }
    }
}

