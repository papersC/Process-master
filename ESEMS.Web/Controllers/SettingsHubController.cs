using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Workflow;
using ESEMS.Web.Models.Import;
using ESEMS.Web.Security;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Settings Hub — central administration page with a six-tab UI inspired
/// by the PManagement Settings Hub. Owns the approval rules, the generic
/// AppSettings key/value store (which backs General/Email/Alerts/AI tabs),
/// and the data-import forwarder.
/// </summary>
[Authorize(Policy = AppPolicies.CanAdmin)]
public class SettingsHubController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<SettingsHubController> _logger;
    private readonly IWebHostEnvironment _env;

    /// <summary>
    /// Canonical list of entity types that flow through ApprovalConfiguration.
    /// Adding an entity here surfaces it on the Approvals tab automatically.
    /// </summary>
    public static readonly (string Key, string LabelEn, string LabelAr, string Icon, string Color)[] ApprovableEntities = new[]
    {
        ("Improvement",   "Improvement Initiative", "مبادرة تحسين",             "trending-up",            "#005B99"),
        ("ChangeRequest", "Change Request",         "طلب تغيير",                "git-pull-request-arrow", "#005B99"),
    };

    private readonly ESEMS.Web.Services.Email.IEmailService? _email;
    private readonly ESEMS.Web.Services.Import.IExcelImportService _importer;

    public SettingsHubController(
        ApplicationDbContext context,
        ILogger<SettingsHubController> logger,
        IWebHostEnvironment env,
        ESEMS.Web.Services.Email.IEmailService email,
        ESEMS.Web.Services.Import.IExcelImportService importer)
    {
        _context = context;
        _logger = logger;
        _env = env;
        _email = email;
        _importer = importer;
    }

    /// <summary>
    /// Admin-triggered SMTP self-test. Reads config from the AppSettings
    /// rows (same ones the Email & Alerts tab writes to) and sends a
    /// probe message to the configured From-address. Returns a friendly
    /// message either way — the UI toasts it.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestSmtp()
    {
        if (_email == null)
            return Json(new { success = false, message = "Email service is not configured." });

        var (ok, message) = await _email.TestConnectionAsync();
        return Json(new { success = ok, message });
    }

    // ═══════════════════════════════════════════════════════════════════
    // INDEX — renders all six tabs
    // ═══════════════════════════════════════════════════════════════════

    public async Task<IActionResult> Index(string tab = "general")
    {
        ViewBag.ActiveTab = tab;
        ViewBag.Environment = _env.EnvironmentName;

        ViewBag.Configs = await _context.ApprovalConfigurations
            .AsNoTracking()
            .OrderBy(c => c.EntityType)
            .ThenBy(c => c.Priority)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync();

        ViewBag.Settings = await _context.AppSettings
            .AsNoTracking()
            .OrderBy(s => s.Category)
            .ThenBy(s => s.Key)
            .ToListAsync();

        // Role groups + module/action catalog for the permission matrix tab
        ViewBag.RoleGroups = await _context.RoleGroups
            .AsNoTracking()
            .OrderBy(g => g.NameEn)
            .ToListAsync();
        ViewBag.ModuleCatalog = RoleGroupsController.ModuleCatalog;
        ViewBag.AllActions = RoleGroupsController.AllActions;

        // Recent audit trail for the Activity Logs tab (top 50, newest first).
        ViewBag.RecentAuditLogs = await _context.AuditLogs
            .AsNoTracking()
            .OrderByDescending(l => l.Timestamp)
            .Take(50)
            .ToListAsync();

        ViewBag.Users = await _context.Users
            .AsNoTracking()
            .OrderBy(u => u.EmployeeName)
            .Select(u => new
            {
                Id = u.UserId,
                FullNameEn = u.EmployeeName ?? u.Username,
                FullNameAr = u.EmployeeNameAr ?? u.EmployeeName ?? u.Username,
                Email = u.EmailAddress ?? "",
                DeptEn = u.Department ?? u.DirectOrgNameEn ?? "",
                DeptAr = u.DepartmentAr ?? u.DirectOrgNameAr ?? "",
                JobTitle = u.JobName ?? ""
            })
            .ToListAsync();

        ViewBag.Entities = ApprovableEntities;

        // System Info tab — read-only counters across every module so admins
        // and auditors can see a one-page snapshot of the system's state.
        ViewBag.TotalProcesses = await _context.Processes.CountAsync(p => !p.IsDeleted);
        ViewBag.TotalServices = await _context.Services.CountAsync(s => !s.IsDeleted);
        ViewBag.TotalUnits = await _context.OrganizationUnits.CountAsync();
        ViewBag.TotalImprovements = await _context.ImprovementInitiatives.CountAsync(i => !i.IsDeleted);
        ViewBag.ProposedImprovements = await _context.ImprovementInitiatives.CountAsync(i => !i.IsDeleted && i.Status == ImprovementStatus.Proposed);
        ViewBag.InProgressImprovements = await _context.ImprovementInitiatives.CountAsync(i => !i.IsDeleted && i.Status == ImprovementStatus.InProgress);
        ViewBag.ClosedImprovements = await _context.ImprovementInitiatives.CountAsync(i => !i.IsDeleted && i.Status == ImprovementStatus.Closed);
        ViewBag.TotalActualSavings = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted && i.ActualCostSavings.HasValue)
            .SumAsync(i => i.ActualCostSavings ?? 0m);
        ViewBag.TotalEstimatedSavings = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted && i.EstimatedCostSavings.HasValue)
            .SumAsync(i => i.EstimatedCostSavings ?? 0m);
        ViewBag.TotalEstimatedHours = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted && i.EstimatedTimeSavings.HasValue)
            .SumAsync(i => i.EstimatedTimeSavings ?? 0m);
        ViewBag.TotalActualHours = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted && i.ActualTimeSavings.HasValue)
            .SumAsync(i => i.ActualTimeSavings ?? 0m);
        ViewBag.TotalRisks = await _context.EnterpriseRisks.CountAsync();
        ViewBag.HighRisks = await _context.EnterpriseRisks.CountAsync(r => r.RiskLevel == RiskLevel.High || r.RiskLevel == RiskLevel.Critical);
        ViewBag.TotalAssets = await _context.Assets.CountAsync();
        ViewBag.TotalIncidents = await _context.Incidents.CountAsync();
        ViewBag.OpenIncidents = await _context.Incidents.CountAsync(i => i.Status == IncidentStatus.New || i.Status == IncidentStatus.Acknowledged || i.Status == IncidentStatus.InProgress || i.Status == IncidentStatus.OnHold);
        ViewBag.TotalUsers = await _context.Users.CountAsync();
        ViewBag.PendingWorkflows = await _context.WorkflowInstances.CountAsync(w => w.Status == WorkflowStatus.Submitted || w.Status == WorkflowStatus.UnderReview);
        ViewBag.ApprovalRules = await _context.ApprovalConfigurations.CountAsync(c => c.IsActive);
        ViewBag.RoleGroupsCount = await _context.RoleGroups.CountAsync(g => g.IsActive);

        return View();
    }

    // ═══════════════════════════════════════════════════════════════════
    // APPROVALS TAB — rule CRUD
    // ═══════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRule([FromForm] SaveRuleRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.EntityType))
            return Json(new { success = false, error = "EntityType is required." });
        if (!ApprovableEntities.Any(e => e.Key == req.EntityType))
            return Json(new { success = false, error = $"Unknown entity type '{req.EntityType}'." });

        // I4: validate the Level-1 approver — the UI marks it Required but the
        // server previously accepted an empty Level1ApproverUserId, persisting
        // a rule with NULL approver (so no item could ever route to anyone).
        // SpecificUser is the default ApproverType, so an empty UserId there is
        // the silent-NULL case. Other approver types (e.g. RoleGroup) don't
        // require a UserId and are left to their own validators.
        var l1Type = string.IsNullOrWhiteSpace(req.Level1ApproverType) ? "SpecificUser" : req.Level1ApproverType;
        if (l1Type == "SpecificUser" && (req.Level1ApproverUserId == null || req.Level1ApproverUserId <= 0))
            return Json(new { success = false, error = "Level 1 approver is required." });

        // Same guard on Level 2 IF the caller asked for it.
        if (req.Level2Required)
        {
            var l2Type = string.IsNullOrWhiteSpace(req.Level2ApproverType) ? "SpecificUser" : req.Level2ApproverType;
            if (l2Type == "SpecificUser" && (req.Level2ApproverUserId == null || req.Level2ApproverUserId <= 0))
                return Json(new { success = false, error = "Level 2 approver is required when Level 2 is enabled." });
        }

        try
        {
            ApprovalConfiguration rule;
            if (!string.IsNullOrWhiteSpace(req.Id))
            {
                rule = await _context.ApprovalConfigurations.FirstOrDefaultAsync(c => c.Id == req.Id)
                    ?? throw new InvalidOperationException("Rule not found.");
            }
            else
            {
                rule = new ApprovalConfiguration { EntityType = req.EntityType };
                _context.ApprovalConfigurations.Add(rule);
            }

            rule.EntityType = req.EntityType;
            rule.RuleName = string.IsNullOrWhiteSpace(req.RuleName) ? null : req.RuleName.Trim();
            rule.Priority = req.Priority <= 0 ? 100 : req.Priority;

            rule.MinCostSavings = req.MinCostSavings;
            rule.MaxCostSavings = req.MaxCostSavings;
            rule.MinImpactScore = req.MinImpactScore;
            rule.MaxImpactScore = req.MaxImpactScore;
            rule.MinDurationDays = req.MinDurationDays;
            rule.MaxDurationDays = req.MaxDurationDays;
            rule.Horizon = string.IsNullOrWhiteSpace(req.Horizon) ? null : req.Horizon;
            rule.InnovationType = string.IsNullOrWhiteSpace(req.InnovationType) ? null : req.InnovationType;

            rule.Level1Required = true;
            rule.Level1ApproverType = string.IsNullOrWhiteSpace(req.Level1ApproverType) ? "SpecificUser" : req.Level1ApproverType;
            rule.Level1ApproverUserId = rule.Level1ApproverType == "SpecificUser" ? req.Level1ApproverUserId : null;
            rule.Level1ApproverName = await ResolveUserNameAsync(rule.Level1ApproverUserId);

            rule.Level2Required = req.Level2Required;
            rule.Level2ApproverType = req.Level2Required
                ? (string.IsNullOrWhiteSpace(req.Level2ApproverType) ? "SpecificUser" : req.Level2ApproverType)
                : null;
            rule.Level2ApproverUserId = req.Level2Required && rule.Level2ApproverType == "SpecificUser"
                ? req.Level2ApproverUserId
                : null;
            rule.Level2ApproverName = await ResolveUserNameAsync(rule.Level2ApproverUserId);

            // SLA + escalation — all nullable; leaving a field blank means
            // "no SLA on that level" / "no escalation target".
            rule.Level1SlaHours = req.Level1SlaHours;
            rule.Level2SlaHours = req.Level2SlaHours;
            rule.EscalationUserId = req.EscalationUserId;
            rule.EscalationUserName = await ResolveUserNameAsync(rule.EscalationUserId);

            rule.IsActive = true;
            rule.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Json(new { success = true, id = rule.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save approval rule for {EntityType}", req.EntityType);
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRule(string id)
    {
        try
        {
            var rule = await _context.ApprovalConfigurations.FirstOrDefaultAsync(c => c.Id == id);
            if (rule != null)
            {
                _context.ApprovalConfigurations.Remove(rule);
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete approval rule {Id}", id);
            return Json(new { success = false, error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // GENERAL / EMAIL / INTEGRATIONS TABS — AppSettings CRUD
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Bulk upsert — used by Email, Alerts, AI tabs that save many keys at once.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveBulkSettings([FromForm] List<string> Keys, [FromForm] List<string> Values)
    {
        if (Keys == null || Values == null || Keys.Count != Values.Count)
            return Json(new { success = false, error = "Keys/Values length mismatch." });

        try
        {
            for (var i = 0; i < Keys.Count; i++)
            {
                var key = Keys[i]?.Trim();
                if (string.IsNullOrWhiteSpace(key)) continue;
                var value = Values[i];

                var existing = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
                if (existing == null)
                {
                    _context.AppSettings.Add(new AppSetting
                    {
                        Key = key,
                        Value = value,
                        Category = key.Contains('.') ? key.Substring(0, key.IndexOf('.')) : "General",
                        DataType = InferDataType(value),
                        Hidden = true // tab-owned keys don't appear in General
                    });
                }
                else
                {
                    existing.Value = value;
                    existing.UpdatedAt = DateTime.UtcNow;
                }
            }
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save bulk settings");
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>Upsert a single new setting from the General tab's "New Setting" modal.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpsertSetting([FromForm] UpsertSettingRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.Key))
            return Json(new { success = false, error = "Key is required." });

        try
        {
            var existing = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == req.Key);
            if (existing == null)
            {
                existing = new AppSetting { Key = req.Key.Trim() };
                _context.AppSettings.Add(existing);
            }
            existing.Value = req.Value;
            existing.Category = string.IsNullOrWhiteSpace(req.Category) ? "General" : req.Category;
            existing.DataType = string.IsNullOrWhiteSpace(req.DataType) ? "string" : req.DataType;
            existing.DescriptionEn = req.DescriptionEn;
            existing.DescriptionAr = req.DescriptionAr;
            existing.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Json(new { success = true, id = existing.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert setting {Key}", req.Key);
            return Json(new { success = false, error = ex.Message });
        }
    }

    /// <summary>Update only the value of an existing setting (inline save from General tab rows).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSettingValue(string Id, string Value)
    {
        try
        {
            var s = await _context.AppSettings.FirstOrDefaultAsync(x => x.Id == Id);
            if (s == null) return Json(new { success = false, error = "Setting not found." });
            s.Value = Value;
            s.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update setting value {Id}", Id);
            return Json(new { success = false, error = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteSetting(string id)
    {
        try
        {
            var s = await _context.AppSettings.FirstOrDefaultAsync(x => x.Id == id);
            if (s != null)
            {
                _context.AppSettings.Remove(s);
                await _context.SaveChangesAsync();
            }
            return Json(new { success = true });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // INTEGRATIONS — trigger AI relearn
    // ═══════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TriggerRelearn()
    {
        // Placeholder — writes a LastRun marker so the UI can show it.
        // Real implementation would enqueue a job to VectorSyncBackgroundService.
        var key = "Ai.LastRun";
        var existing = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (existing == null)
        {
            _context.AppSettings.Add(new AppSetting
            {
                Key = key,
                Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm"),
                Category = "Ai",
                Hidden = true
            });
        }
        else
        {
            existing.Value = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm");
            existing.UpdatedAt = DateTime.UtcNow;
        }
        await _context.SaveChangesAsync();
        return Json(new { success = true });
    }

    /// <summary>
    /// Probes a sister-system endpoint with the values currently in the form, BEFORE
    /// committing them to AppSettings. Lets the admin verify Base URL + API key are
    /// correct without a full save + recycle. Hits {BaseUrl}/health with the same
    /// X-Api-Key header the live providers send.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TestIntegrationConnection([FromForm] string Kind, [FromForm] string BaseUrl, [FromForm] string? ApiKey, [FromForm] int TimeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(BaseUrl))
            return Json(new { success = false, error = "Base URL is required." });

        var timeout = TimeoutSeconds > 0 && TimeoutSeconds <= 60 ? TimeoutSeconds : 8;
        var url = BaseUrl.EndsWith('/') ? BaseUrl + "health" : BaseUrl + "/health";

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeout) };
            if (!string.IsNullOrWhiteSpace(ApiKey))
                http.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", ApiKey);
            http.DefaultRequestHeaders.TryAddWithoutValidation("Accept", "application/json");
            http.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", "ESEMS-Integration/1.0");
            var r = await http.GetAsync(url);
            return Json(new { success = r.IsSuccessStatusCode, statusCode = (int)r.StatusCode, error = r.IsSuccessStatusCode ? null : $"HTTP {(int)r.StatusCode}" });
        }
        catch (TaskCanceledException)
        {
            return Json(new { success = false, error = $"Timeout after {timeout}s" });
        }
        catch (HttpRequestException ex)
        {
            return Json(new { success = false, error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Integration probe failed for {Kind} at {Url}", Kind, url);
            return Json(new { success = false, error = ex.GetType().Name + ": " + ex.Message });
        }
    }

    // ═══════════════════════════════════════════════════════════════════
    // DATA IMPORT — thin forwarder to ImportController's existing endpoints
    // ═══════════════════════════════════════════════════════════════════

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(50_000_000)]
    public async Task<IActionResult> ImportUpload(string kind, IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return Json(new { success = false, error = "No file selected." });

        // F-012: server-side file-type guard. The client only sets
        // accept=".xlsx,.xls" (trivially bypassed), so previously any file —
        // a PDF, an image, an .exe — streamed straight into the parser and
        // surfaced a raw "Import failed: <exception>". Validate the extension
        // AND the magic bytes (xlsx = ZIP "PK..", xls = OLE2 compound document)
        // and reject anything else with a friendly message up front.
        var ext = System.IO.Path.GetExtension(file.FileName)?.ToLowerInvariant();
        if (ext != ".xlsx" && ext != ".xls")
            return Json(new { success = false, error = "Unsupported file type. Please upload an Excel .xlsx or .xls workbook." });

        var header = new byte[8];
        int headerRead;
        using (var probe = file.OpenReadStream())
            headerRead = await probe.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);

        bool isXlsx = headerRead >= 4 && header[0] == 0x50 && header[1] == 0x4B
                      && (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07);
        bool isXls = headerRead >= 8 && header[0] == 0xD0 && header[1] == 0xCF && header[2] == 0x11 && header[3] == 0xE0
                     && header[4] == 0xA1 && header[5] == 0xB1 && header[6] == 0x1A && header[7] == 0xE1;
        if (!isXlsx && !isXls)
            return Json(new { success = false, error = "The uploaded file is not a valid Excel workbook." });

        var k = (kind ?? "").Trim().ToLowerInvariant();

        using var stream = file.OpenReadStream();
        Services.Import.ImportResult result;
        try
        {
            result = k switch
            {
                "processes"     => await _importer.ImportProcessesAsync(stream, ct),
                "services"      => await _importer.ImportServicesAsync(stream, ct),
                "assets"        => await _importer.ImportAssetsAsync(stream, ct),
                "risks"         => await _importer.ImportRisksAsync(stream, ct),
                "mbrhe-assets"   => await _importer.ImportMbrheAssetRegisterAsync(stream, ct),
                "mbrhe-services" => await _importer.ImportMbrheServicesCatalogAsync(stream, ct),
                "mbrhe-org"      => await _importer.ImportMbrheOrgStructureAsync(stream, ct),
                "mbrhe-apqc"     => await _importer.ImportApqcProcessMappingAsync(stream, ct),
                _ => null!
            };
            if (result == null)
                return Json(new { success = false, error = $"Unknown import kind '{kind}'." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed for kind={Kind} file={FileName}", k, file.FileName);
            return Json(new { success = false, error = "Import failed: " + ex.Message });
        }

        if (result.FatalError != null)
            return Json(new { success = false, error = result.FatalError });

        // Warnings (soft cross-table dependency misses) do NOT fail the import.
        var ok = result.Errors.Count == 0;

        var msg = $"Imported {result.Imported} row(s)";
        if (result.Skipped > 0) msg += $", skipped {result.Skipped} (already exist)";
        if (result.Warnings.Count > 0) msg += $", {result.Warnings.Count} warning(s)";
        if (result.Errors.Count > 0) msg += $", {result.Errors.Count} row error(s)";
        msg += ".";

        // Append a short preview to the toast — prefer hard errors; if there
        // are none, preview the warnings (e.g. unresolved owner departments).
        var previewList = result.Errors.Count > 0 ? result.Errors : result.Warnings;
        if (previewList.Count > 0)
        {
            var preview = string.Join("; ", previewList.Take(3).Select(e => $"row {e.Row}: {e.Message}"));
            if (previewList.Count > 3) preview += $" (+{previewList.Count - 3} more)";
            msg += " " + preview;
        }

        // Record the run so it can be undone from the "Recent imports" panel.
        // Only legacy importers populate Created, so standard template uploads
        // produce no batch (and get no delete button).
        string? batchId = null;
        if (result.Created.Count > 0)
        {
            var manifest = System.Text.Json.JsonSerializer.Serialize(
                result.Created.Select(c => new ManifestRow(c.Table, c.Id)).ToList());
            var batch = new ImportBatch
            {
                Kind = k,
                FileName = file.FileName,
                ImportedCount = result.Imported,
                SkippedCount = result.Skipped,
                Manifest = manifest,
                CreatedByName = User?.Identity?.Name,
                CreatedAt = DateTime.UtcNow
            };
            _context.ImportBatches.Add(batch);
            await _context.SaveChangesAsync(ct);
            batchId = batch.Id;
        }

        return Json(new
        {
            success = ok,
            imported = result.Imported,
            skipped = result.Skipped,
            warnings = result.Warnings.Select(e => new { row = e.Row, message = e.Message }),
            errors = result.Errors.Select(e => new { row = e.Row, message = e.Message }),
            message = msg,
            error = ok ? null : msg,
            batchId
        });
    }

    // ═══════════════════════════════════════════════════════════════════
    // IMPORT UNDO — list recent legacy import runs and revert one
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Manifest row shape: one created database row, table + id.</summary>
    private sealed record ManifestRow(string T, string Id);

    /// <summary>Non-reverted import runs, newest first, for the Data tab panel.</summary>
    [HttpGet]
    public async Task<IActionResult> RecentImports(CancellationToken ct)
    {
        var batches = await _context.ImportBatches.AsNoTracking()
            .Where(b => !b.IsReverted)
            .OrderByDescending(b => b.CreatedAt)
            .Take(25)
            .Select(b => new { b.Id, b.Kind, b.FileName, b.ImportedCount, b.SkippedCount, b.CreatedAt, b.CreatedByName })
            .ToListAsync(ct);

        bool ar = System.Globalization.CultureInfo.CurrentUICulture.Name.StartsWith("ar");
        var items = batches.Select(b => new
        {
            id = b.Id,
            kind = b.Kind,
            kindLabel = LegacyKindLabel(b.Kind, ar),
            fileName = b.FileName,
            imported = b.ImportedCount,
            skipped = b.SkippedCount,
            createdAt = b.CreatedAt,
            createdBy = b.CreatedByName
        });
        return Json(new { success = true, items });
    }

    /// <summary>
    /// Undo one import run by hard-deleting every row in its manifest, child
    /// before parent, inside a transaction. Parent rows that have acquired an
    /// out-of-batch dependent since the import (a process under an imported
    /// group, an asset assigned to an imported unit, etc.) are kept so nothing
    /// is orphaned. Hard delete (not soft) is deliberate: it frees the
    /// AssetTag/Code unique indexes so the same file re-imports cleanly.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevertImport(string id, CancellationToken ct)
    {
        var batch = await _context.ImportBatches.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (batch == null) return Json(new { success = false, error = "Import not found." });
        if (batch.IsReverted) return Json(new { success = false, error = "This import has already been undone." });

        List<ManifestRow> rows;
        try { rows = System.Text.Json.JsonSerializer.Deserialize<List<ManifestRow>>(batch.Manifest) ?? new(); }
        catch { return Json(new { success = false, error = "Import manifest is corrupt; undo it manually." }); }

        HashSet<string> Ids(string table) => rows
            .Where(r => string.Equals(r.T, table, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var respIds    = Ids("OrganizationUnitResponsibilities");
        var unitIds    = Ids("OrganizationUnits");
        var procIds    = Ids("Processes");
        var pgIds      = Ids("ProcessGroups");
        var catIds     = Ids("Categories");
        var catalogIds = Ids("ServiceCatalogInfos");
        var svcIds     = Ids("Services");
        var svcCatIds  = Ids("ServiceCategories");
        var assetIds   = Ids("Assets");
        var riskIds    = Ids("EnterpriseRisks"); // F-024: standard Risks template undo

        int deleted = 0, kept = 0;

        // Same retry-strategy guard as WorkflowController.ProcessAction —
        // EnableRetryOnFailure rejects raw BeginTransactionAsync calls.
        // Wrapping the whole revert in ExecuteAsync lets the strategy retry
        // the (idempotent: every "is anything still referencing this" check
        // happens inside the txn) work after a transient SQL failure.
        var strategy = _context.Database.CreateExecutionStrategy();
        IActionResult? earlyExit = null;
        await strategy.ExecuteAsync(async () =>
        {
            // Reset on retry so a partial first attempt doesn't double-count.
            deleted = 0; kept = 0;
            earlyExit = null;

            await using var tx = await _context.Database.BeginTransactionAsync(ct);
            try
            {
            // 1. Leaf children first.
            if (respIds.Count > 0)
            {
                var del = await _context.OrganizationUnitResponsibilities.Where(r => respIds.Contains(r.Id)).ToListAsync(ct);
                _context.OrganizationUnitResponsibilities.RemoveRange(del);
                await _context.SaveChangesAsync(ct);
                deleted += del.Count;
            }

            if (catalogIds.Count > 0)
            {
                var del = await _context.ServiceCatalogInfos.Where(c => catalogIds.Contains(c.Id)).ToListAsync(ct);
                _context.ServiceCatalogInfos.RemoveRange(del);
                await _context.SaveChangesAsync(ct);
                deleted += del.Count;
            }

            // 2. Processes — keep any still referenced by an Activity.
            if (procIds.Count > 0)
            {
                var inUse = (await _context.Activities
                        .Where(a => procIds.Contains(a.ProcessId))
                        .Select(a => a.ProcessId).Distinct().ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var del = await _context.Processes.Where(p => procIds.Contains(p.Id) && !inUse.Contains(p.Id)).ToListAsync(ct);
                _context.Processes.RemoveRange(del);
                await _context.SaveChangesAsync(ct);
                deleted += del.Count;
                kept += procIds.Count(pid => inUse.Contains(pid));
            }

            // 3. ProcessGroups — keep any that still have a process under them.
            if (pgIds.Count > 0)
            {
                var inUse = (await _context.Processes.Where(p => pgIds.Contains(p.ProcessGroupId))
                        .Select(p => p.ProcessGroupId).Distinct().ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var del = await _context.ProcessGroups.Where(g => pgIds.Contains(g.Id) && !inUse.Contains(g.Id)).ToListAsync(ct);
                _context.ProcessGroups.RemoveRange(del);
                await _context.SaveChangesAsync(ct);
                deleted += del.Count;
                kept += pgIds.Count(gid => inUse.Contains(gid));
            }

            // 4. Categories — keep any that still have a process group under them.
            if (catIds.Count > 0)
            {
                var inUse = (await _context.ProcessGroups.Where(g => catIds.Contains(g.CategoryId))
                        .Select(g => g.CategoryId).Distinct().ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var del = await _context.Categories.Where(c => catIds.Contains(c.Id) && !inUse.Contains(c.Id)).ToListAsync(ct);
                _context.Categories.RemoveRange(del);
                await _context.SaveChangesAsync(ct);
                deleted += del.Count;
                kept += catIds.Count(cid => inUse.Contains(cid));
            }

            // 5. Services (catalog already removed above; cascade is a no-op).
            if (svcIds.Count > 0)
            {
                var del = await _context.Services.Where(s => svcIds.Contains(s.Id)).ToListAsync(ct);
                _context.Services.RemoveRange(del);
                await _context.SaveChangesAsync(ct);
                deleted += del.Count;
            }

            // 6. ServiceCategories — keep any that still have a service under them.
            if (svcCatIds.Count > 0)
            {
                var inUse = (await _context.Services.Where(s => s.ServiceCategoryId != null && svcCatIds.Contains(s.ServiceCategoryId))
                        .Select(s => s.ServiceCategoryId!).Distinct().ToListAsync(ct))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var del = await _context.ServiceCategories.Where(c => svcCatIds.Contains(c.Id) && !inUse.Contains(c.Id)).ToListAsync(ct);
                _context.ServiceCategories.RemoveRange(del);
                await _context.SaveChangesAsync(ct);
                deleted += del.Count;
                kept += svcCatIds.Count(cid => inUse.Contains(cid));
            }

            // 7. Assets.
            if (assetIds.Count > 0)
            {
                var del = await _context.Assets.Where(a => assetIds.Contains(a.Id)).ToListAsync(ct);
                _context.Assets.RemoveRange(del);
                await _context.SaveChangesAsync(ct);
                deleted += del.Count;
            }

            // 7b. EnterpriseRisks (F-024). Plain delete like Assets — a risk
            // that's since been linked (asset-risk, improvement-risk, treatment)
            // hits an FK constraint and the whole revert rolls back with the
            // friendly "now referenced" message via the DbUpdateException catch.
            if (riskIds.Count > 0)
            {
                var del = await _context.EnterpriseRisks.Where(r => riskIds.Contains(r.Id)).ToListAsync(ct);
                _context.EnterpriseRisks.RemoveRange(del);
                await _context.SaveChangesAsync(ct);
                deleted += del.Count;
            }

            // 8. OrganizationUnits — self-referencing tree. Prune leaves
            // repeatedly: a unit is deletable only once it has no child unit,
            // no responsibility, and no asset assigned to it. Units that keep
            // an out-of-batch dependent survive (counted as kept).
            // OrganizationUnit.Id is int now; the manifest stores ids as
            // strings, so parse them before comparing against the int FKs
            // (ParentId / AssignedToUnitId / OrganizationUnitId).
            if (unitIds.Count > 0)
            {
                var remaining = unitIds
                    .Select(s => int.TryParse(s, out var v) ? (int?)v : null)
                    .Where(v => v != null)
                    .Select(v => v!.Value)
                    .ToHashSet();
                bool progress = true;
                while (progress && remaining.Count > 0)
                {
                    progress = false;
                    var deletableThisPass = new List<int>();
                    foreach (var uid in remaining)
                    {
                        bool hasChild = await _context.OrganizationUnits.AnyAsync(u => u.ParentId == uid, ct);
                        bool hasResp  = await _context.OrganizationUnitResponsibilities.AnyAsync(r => r.OrganizationUnitId == uid, ct);
                        bool hasAsset = await _context.Assets.AnyAsync(a => a.AssignedToUnitId == uid, ct);
                        if (!hasChild && !hasResp && !hasAsset) deletableThisPass.Add(uid);
                    }
                    if (deletableThisPass.Count > 0)
                    {
                        var del = await _context.OrganizationUnits.Where(u => deletableThisPass.Contains(u.Id)).ToListAsync(ct);
                        _context.OrganizationUnits.RemoveRange(del);
                        await _context.SaveChangesAsync(ct);
                        deleted += del.Count;
                        foreach (var uid in deletableThisPass) remaining.Remove(uid);
                        progress = true;
                    }
                }
                kept += remaining.Count;
            }

            batch.IsReverted = true;
            batch.RevertedAt = DateTime.UtcNow;
            batch.RevertedCount = deleted;
            await _context.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                // No explicit RollbackAsync — `await using` disposes the txn
                // (which rolls back) on the way out.
                _logger.LogWarning(ex, "RevertImport failed for batch {BatchId}", id);
                earlyExit = Json(new { success = false, error = "Couldn't undo — some imported rows are now referenced by other records. Remove those links first, then try again." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RevertImport error for batch {BatchId}", id);
                earlyExit = Json(new { success = false, error = "Undo failed: " + ex.Message });
            }
        });

        if (earlyExit != null) return earlyExit;

        var msg = $"Undone — deleted {deleted} row(s)";
        if (kept > 0) msg += $", kept {kept} still referenced by other data";
        msg += ".";
        return Json(new { success = true, deleted, kept, message = msg });
    }

    private static string LegacyKindLabel(string kind, bool ar) => kind switch
    {
        "mbrhe-assets"   => ar ? "سجل أصول المؤسسة (MBRHE)"        : "MBRHE Asset Register",
        "mbrhe-services" => ar ? "كتالوج خدمات المؤسسة (MBRHE)"    : "MBRHE Services Catalog",
        "mbrhe-org"      => ar ? "الهيكل التنظيمي والمهام (MBRHE)" : "MBRHE Org Structure + Tasks",
        "mbrhe-apqc"     => ar ? "خريطة عمليات APQC"               : "APQC Process Mapping",
        "processes"      => ar ? "العمليات" : "Processes",
        "services"       => ar ? "الخدمات"  : "Services",
        "assets"         => ar ? "الأصول"   : "Assets",
        "risks"          => ar ? "المخاطر"  : "Risks",
        _ => kind
    };

    // ═══════════════════════════════════════════════════════════════════
    // Helpers
    // ═══════════════════════════════════════════════════════════════════

    private async Task<string?> ResolveUserNameAsync(int? userId)
    {
        if (!userId.HasValue) return null;
        return await _context.Users.AsNoTracking()
            .Where(x => x.UserId == userId.Value)
            .Select(x => x.EmployeeName ?? x.Username)
            .FirstOrDefaultAsync();
    }

    private static string InferDataType(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "string";
        if (bool.TryParse(value, out _)) return "bool";
        if (int.TryParse(value, out _)) return "int";
        if (decimal.TryParse(value, out _)) return "decimal";
        return "string";
    }

    // ═══════════════════════════════════════════════════════════════════
    // Request DTOs
    // ═══════════════════════════════════════════════════════════════════

    public class SaveRuleRequest
    {
        public string? Id { get; set; }
        public string EntityType { get; set; } = string.Empty;
        public string? RuleName { get; set; }
        public int Priority { get; set; } = 100;
        public decimal? MinCostSavings { get; set; }
        public decimal? MaxCostSavings { get; set; }
        public int? MinImpactScore { get; set; }
        public int? MaxImpactScore { get; set; }
        public int? MinDurationDays { get; set; }
        public int? MaxDurationDays { get; set; }
        public string? Horizon { get; set; }
        public string? InnovationType { get; set; }
        public string? Level1ApproverType { get; set; }
        public int? Level1ApproverUserId { get; set; }
        public bool Level2Required { get; set; }
        public string? Level2ApproverType { get; set; }
        public int? Level2ApproverUserId { get; set; }
        public int? Level1SlaHours { get; set; }
        public int? Level2SlaHours { get; set; }
        public int? EscalationUserId { get; set; }
    }

    public class UpsertSettingRequest
    {
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? Category { get; set; }
        public string? DataType { get; set; }
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
    }
}
