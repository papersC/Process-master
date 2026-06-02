using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers.Api;

/// <summary>
/// OUTBOUND read-only API: lets other systems on the MBRHE server consume data
/// FROM this ESEMS instance. Authenticated by the <c>X-Api-Key</c> header
/// (<see cref="ApiKeyRequiredAttribute"/>), mirroring how ESEMS authenticates
/// when it READS from the external Risk / Performance systems.
///
/// Read-only by construction: every endpoint is <c>[HttpGet]</c>; there is no
/// create/update/delete surface. The risk endpoints emit the SAME JSON shape
/// (<c>{ "risks": [...] }</c>) that ESEMS's own <c>RiskHttpProvider</c>
/// consumes, so another ESEMS instance can point its
/// <c>Integrations:Risk:BaseUrl</c> at <c>{thisHost}/api/v1</c> and read this
/// system as its external risk source.
///
/// <c>[AllowAnonymous]</c> bypasses the global cookie FallbackPolicy so API-key
/// callers (which carry no auth cookie) aren't redirected to the login page;
/// <see cref="ApiKeyRequiredAttribute"/> is the real gate.
/// </summary>
[ApiController]
[Route("api/v1")]
[AllowAnonymous]
[ApiKeyRequired]
[Produces("application/json")]
public sealed class ExternalDataController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ExternalDataController(ApplicationDbContext db) => _db = db;

    /// <summary>Liveness/health probe — matches the GET {BaseUrl}/health the
    /// integration clients expect, so this instance can be probed by consumers.</summary>
    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(new { status = "ok", name = "ESEMS", utc = DateTime.UtcNow });

    [HttpGet("processes")]
    public async Task<IActionResult> Processes(int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        var items = await _db.Processes.AsNoTracking()
            .Where(p => !p.IsDeleted)
            .OrderBy(p => p.Code)
            .Take(take)
            .Select(p => new { id = p.Id, code = p.Code, nameEn = p.NameEn, nameAr = p.NameAr })
            .ToListAsync();
        return Ok(new { count = items.Count, processes = items });
    }

    [HttpGet("services")]
    public async Task<IActionResult> Services(int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        var items = await _db.Services.AsNoTracking()
            .Where(s => !s.IsDeleted)
            .OrderBy(s => s.Code)
            .Take(take)
            .Select(s => new { id = s.Id, code = s.Code, nameEn = s.NameEn, nameAr = s.NameAr, type = s.ServiceType.ToString() })
            .ToListAsync();
        return Ok(new { count = items.Count, services = items });
    }

    /// <summary>All enterprise risks, in the contract shape ESEMS's own
    /// RiskHttpProvider consumes ({ "risks": [...] }).</summary>
    [HttpGet("risks")]
    public async Task<IActionResult> Risks(int take = 100)
    {
        take = Math.Clamp(take, 1, 500);
        var items = await _db.EnterpriseRisks.AsNoTracking()
            .Where(r => !r.IsDeleted)
            .OrderBy(r => r.RiskNumber)
            .Take(take)
            .Select(r => new
            {
                id = r.Id,
                code = r.RiskNumber,
                title = r.NameEn,
                severity = r.RiskLevel.ToString(),
                status = r.IsActive ? "Active" : "Inactive",
                ownerName = (string?)null,
                createdAt = r.CreatedAt
            })
            .ToListAsync();
        return Ok(new { risks = items });
    }

    /// <summary>Risks linked to one ESEMS entity, in the same { "risks": [...] }
    /// shape as GET {BaseUrl}/risks/by-entity. Supports type=service|process.</summary>
    [HttpGet("risks/by-entity")]
    public async Task<IActionResult> RisksByEntity(string type, string id, int take = 25)
    {
        take = Math.Clamp(take, 1, 200);
        if (string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(id))
            return BadRequest(new { error = "type and id are required." });

        IQueryable<Models.RiskManagement.EnterpriseRisk> q = type.Trim().ToLowerInvariant() switch
        {
            "service" => _db.EnterpriseRisks.AsNoTracking()
                            .Where(r => !r.IsDeleted && _db.ServiceRisks
                                .Any(sr => sr.ServiceId == id && sr.IsActive && sr.RiskId == r.Id)),
            "process" => _db.EnterpriseRisks.AsNoTracking()
                            .Where(r => r.ProcessId == id && !r.IsDeleted),
            _ => Enumerable.Empty<Models.RiskManagement.EnterpriseRisk>().AsQueryable()
        };

        var items = await q
            .OrderBy(r => r.RiskNumber)
            .Take(take)
            .Select(r => new
            {
                id = r.Id,
                code = r.RiskNumber,
                title = r.NameEn,
                severity = r.RiskLevel.ToString(),
                status = r.IsActive ? "Active" : "Inactive",
                ownerName = (string?)null,
                createdAt = r.CreatedAt
            })
            .ToListAsync();

        return Ok(new { risks = items });
    }
}
