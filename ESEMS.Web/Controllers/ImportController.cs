using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using ESEMS.Web.Data;
using ESEMS.Web.Security;
using System.Text;
using ClosedXML.Excel;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Controller for Import/Export functionality
/// </summary>
[Authorize(Policy = AppPolicies.CanAdmin)]
public class ImportController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<ImportController> _logger;
    private readonly IStringLocalizer<SharedResource> _localizer;

    public ImportController(ApplicationDbContext context, ILogger<ImportController> logger, IStringLocalizer<SharedResource> localizer)
    {
        _context = context;
        _logger = logger;
        _localizer = localizer;
    }

    /// <summary>
    /// Import/Export Dashboard
    /// </summary>
    public IActionResult Index()
    {
        return View();
    }

    #region Export Templates
    // ────────────────────────────────────────────────────────────────────
    // Column names here MUST stay in lock-step with the headers expected
    // by ExcelImportService — that contract is what makes the round-trip
    // (download → fill → upload) work. Each template ships with an
    // "Instructions" sheet documenting required columns, enum values, and
    // where to find FK codes (Process Groups, Asset Categories, etc.).
    // ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Download Processes import template
    /// </summary>
    public async Task<IActionResult> DownloadProcessesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Processes");
        var headers = new[]
        {
            "Code", "Name (English)", "Name (Arabic)", "Description (English)",
            "Description (Arabic)", "Process Group Code", "Status", "Process Type"
        };
        WriteHeader(ws, headers);

        var groupCodes = await _context.ProcessGroups.AsNoTracking()
            .Where(g => !g.IsDeleted).OrderBy(g => g.Code)
            .Select(g => new LookupRow(g.Code, g.NameEn)).ToListAsync();

        AddInstructionsSheet(workbook, "Processes",
            required: new[] { "Code", "Name (English)", "Name (Arabic)", "Process Group Code" },
            enumDocs: new[]
            {
                ("Status", string.Join(", ", Enum.GetNames<Models.Enums.ProcessStatus>()), "Draft"),
                ("Process Type", string.Join(", ", Enum.GetNames<Models.Enums.ProcessType>()), "Core")
            },
            lookupTitle: "Process Group Codes",
            lookups: groupCodes);

        return BuildTemplateFile(workbook, "Processes_Template.xlsx");
    }

    /// <summary>
    /// Download Services import template
    /// </summary>
    public async Task<IActionResult> DownloadServicesTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Services");
        var headers = new[]
        {
            "Code", "Name (English)", "Name (Arabic)", "Description (English)",
            "Description (Arabic)", "Service Type", "Owning Unit Code", "Service Category Code"
        };
        WriteHeader(ws, headers);

        var unitCodes = await _context.OrganizationUnits.AsNoTracking()
            .Where(u => !u.IsDeleted).OrderBy(u => u.Code)
            .Select(u => new LookupRow(u.Code, u.NameEn)).ToListAsync();

        var categoryCodes = await _context.ServiceCategories.AsNoTracking()
            .Where(c => !c.IsDeleted).OrderBy(c => c.Code)
            .Select(c => new LookupRow(c.Code, c.NameEn)).ToListAsync();

        AddInstructionsSheet(workbook, "Services",
            required: new[] { "Code", "Name (English)", "Name (Arabic)" },
            enumDocs: new[]
            {
                ("Service Type", string.Join(", ", Enum.GetNames<Models.Enums.ServiceType>()), "External")
            },
            lookupTitle: "Owning Unit Codes (optional column)",
            lookups: unitCodes,
            extraNotes: new[]
            {
                "Service Category Code: optional. Must match a Code from Settings → Service Categories. "
                    + (categoryCodes.Count > 0
                        ? "Examples: " + string.Join(", ", categoryCodes.Take(5).Select(c => $"{c.Code} ({c.Name})")) + "."
                        : "Manage categories in the Service Categories settings page.")
            });

        return BuildTemplateFile(workbook, "Services_Template.xlsx");
    }

    /// <summary>
    /// Download Assets import template
    /// </summary>
    public async Task<IActionResult> DownloadAssetsTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Assets");
        var headers = new[]
        {
            "Asset Tag", "Name (English)", "Name (Arabic)", "Description (English)",
            "Description (Arabic)", "Category Code", "Status", "Purchase Date", "Purchase Cost"
        };
        WriteHeader(ws, headers);

        var categoryCodes = await _context.AssetCategories.AsNoTracking()
            .Where(c => !c.IsDeleted).OrderBy(c => c.Code)
            .Select(c => new LookupRow(c.Code, c.NameEn)).ToListAsync();

        AddInstructionsSheet(workbook, "Assets",
            required: new[] { "Asset Tag", "Name (English)", "Name (Arabic)", "Category Code" },
            enumDocs: new[]
            {
                ("Status", string.Join(", ", Enum.GetNames<Models.Enums.AssetStatus>()), "Planned")
            },
            lookupTitle: "Asset Category Codes",
            lookups: categoryCodes,
            extraNotes: new[]
            {
                "Purchase Date: ISO format YYYY-MM-DD (e.g. 2026-01-15).",
                "Purchase Cost: numeric, decimals allowed (e.g. 1500 or 1500.50)."
            });

        return BuildTemplateFile(workbook, "Assets_Template.xlsx");
    }

    /// <summary>
    /// Download Risks import template
    /// </summary>
    public async Task<IActionResult> DownloadRisksTemplate()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Risks");
        var headers = new[]
        {
            "Risk Number", "Name (English)", "Name (Arabic)", "Description (English)",
            "Description (Arabic)", "Category Code", "Likelihood (1-5)", "Impact (1-5)"
        };
        WriteHeader(ws, headers);

        var categoryCodes = await _context.RiskCategories.AsNoTracking()
            .Where(c => !c.IsDeleted).OrderBy(c => c.Code)
            .Select(c => new LookupRow(c.Code, c.NameEn)).ToListAsync();

        AddInstructionsSheet(workbook, "Risks",
            required: new[] { "Risk Number", "Name (English)", "Name (Arabic)", "Category Code", "Likelihood (1-5)", "Impact (1-5)" },
            enumDocs: System.Array.Empty<(string, string, string)>(),
            lookupTitle: "Risk Category Codes",
            lookups: categoryCodes,
            extraNotes: new[]
            {
                "Likelihood: integer 1-5 (1=Rare, 2=Unlikely, 3=Possible, 4=Likely, 5=Almost Certain).",
                "Impact: integer 1-5 (1=Insignificant, 2=Minor, 3=Moderate, 4=Major, 5=Catastrophic).",
                "Inherent Risk Score (Likelihood × Impact) is computed on import."
            });

        return BuildTemplateFile(workbook, "Risks_Template.xlsx");
    }

    // ── Template helpers ────────────────────────────────────────────────

    private sealed record LookupRow(string Code, string Name);

    private static void WriteHeader(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
            ws.Cell(1, i + 1).Value = headers[i];
        var range = ws.Range(1, 1, 1, headers.Length);
        range.Style.Font.Bold = true;
        range.Style.Fill.BackgroundColor = XLColor.FromHtml("#005B99");
        range.Style.Font.FontColor = XLColor.White;
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();
    }

    private static IActionResult BuildTemplateFile(XLWorkbook wb, string fileName)
    {
        using var stream = new MemoryStream();
        wb.SaveAs(stream);
        return new FileContentResult(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
        { FileDownloadName = fileName };
    }

    /// <summary>
    /// Adds a second worksheet documenting the import contract: required
    /// columns, allowed enum values, and the live list of FK codes the user
    /// can put in the lookup column. Pulled from DB so admins always see
    /// the current set without leaving the page.
    /// </summary>
    private static void AddInstructionsSheet(
        XLWorkbook wb,
        string kind,
        string[] required,
        (string Column, string AllowedValues, string Default)[] enumDocs,
        string lookupTitle,
        List<LookupRow> lookups,
        string[]? extraNotes = null)
    {
        var ws = wb.Worksheets.Add("Instructions");
        int row = 1;

        ws.Cell(row, 1).Value = $"{kind} — Import Instructions";
        ws.Cell(row, 1).Style.Font.Bold = true;
        ws.Cell(row, 1).Style.Font.FontSize = 14;
        row += 2;

        ws.Cell(row, 1).Value = "Required columns";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;
        foreach (var col in required)
        {
            ws.Cell(row, 1).Value = "• " + col;
            row++;
        }
        row++;

        ws.Cell(row, 1).Value = "Insert-only";
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;
        ws.Cell(row, 1).Value = "Rows with a key (Code / Asset Tag / Risk Number) that already exists will be skipped, not updated.";
        row += 2;

        if (enumDocs.Length > 0)
        {
            ws.Cell(row, 1).Value = "Allowed values";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            ws.Cell(row, 1).Value = "Column";
            ws.Cell(row, 2).Value = "Allowed values";
            ws.Cell(row, 3).Value = "Default if blank";
            ws.Range(row, 1, row, 3).Style.Font.Bold = true;
            row++;
            foreach (var (col, vals, def) in enumDocs)
            {
                ws.Cell(row, 1).Value = col;
                ws.Cell(row, 2).Value = vals;
                ws.Cell(row, 3).Value = def;
                row++;
            }
            row++;
        }

        if (extraNotes != null && extraNotes.Length > 0)
        {
            ws.Cell(row, 1).Value = "Notes";
            ws.Cell(row, 1).Style.Font.Bold = true;
            row++;
            foreach (var note in extraNotes)
            {
                ws.Cell(row, 1).Value = "• " + note;
                row++;
            }
            row++;
        }

        ws.Cell(row, 1).Value = lookupTitle;
        ws.Cell(row, 1).Style.Font.Bold = true;
        row++;
        if (lookups.Count == 0)
        {
            ws.Cell(row, 1).Value = "(none seeded yet — create the lookup first, then re-download this template)";
            ws.Cell(row, 1).Style.Font.Italic = true;
        }
        else
        {
            ws.Cell(row, 1).Value = "Code";
            ws.Cell(row, 2).Value = "Name";
            ws.Range(row, 1, row, 2).Style.Font.Bold = true;
            row++;
            foreach (var lk in lookups)
            {
                ws.Cell(row, 1).Value = lk.Code;
                ws.Cell(row, 2).Value = lk.Name;
                row++;
            }
        }

        ws.Columns().AdjustToContents();
    }

    #endregion

    #region Export Data

    /// <summary>
    /// Export all assets to Excel
    /// </summary>
    public async Task<IActionResult> ExportAssets()
    {
        var assets = await _context.Assets
            .Include(a => a.Category)
            .OrderBy(a => a.AssetTag)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Assets");

        // Headers
        worksheet.Cell(1, 1).Value = "Asset Tag";
        worksheet.Cell(1, 2).Value = "Name (English)";
        worksheet.Cell(1, 3).Value = "Name (Arabic)";
        worksheet.Cell(1, 4).Value = "Description (English)";
        worksheet.Cell(1, 5).Value = "Description (Arabic)";
        worksheet.Cell(1, 6).Value = "Category";
        worksheet.Cell(1, 7).Value = "Status";
        worksheet.Cell(1, 8).Value = "Purchase Date";
        worksheet.Cell(1, 9).Value = "Purchase Cost";

        // Format header
        var headerRange = worksheet.Range(1, 1, 1, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#005B99");
        headerRange.Style.Font.FontColor = XLColor.White;

        // Data rows
        int row = 2;
        foreach (var asset in assets)
        {
            worksheet.Cell(row, 1).Value = asset.AssetTag;
            worksheet.Cell(row, 2).Value = asset.NameEn;
            worksheet.Cell(row, 3).Value = asset.NameAr;
            worksheet.Cell(row, 4).Value = asset.DescriptionEn;
            worksheet.Cell(row, 5).Value = asset.DescriptionAr;
            worksheet.Cell(row, 6).Value = asset.Category?.NameEn ?? "";
            worksheet.Cell(row, 7).Value = asset.Status.ToString();
            worksheet.Cell(row, 8).Value = asset.PurchaseDate?.ToString("yyyy-MM-dd") ?? "";
            worksheet.Cell(row, 9).Value = asset.PurchaseCost;
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Assets_Export_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// Export all processes to Excel
    /// </summary>
    public async Task<IActionResult> ExportProcesses()
    {
        var processes = await _context.Processes
            .Include(p => p.ProcessGroup)
            .OrderBy(p => p.Code)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Processes");

        // Headers
        worksheet.Cell(1, 1).Value = "Code";
        worksheet.Cell(1, 2).Value = "Name (English)";
        worksheet.Cell(1, 3).Value = "Name (Arabic)";
        worksheet.Cell(1, 4).Value = "Description (English)";
        worksheet.Cell(1, 5).Value = "Description (Arabic)";
        worksheet.Cell(1, 6).Value = "Process Group";
        worksheet.Cell(1, 7).Value = "Status";
        worksheet.Cell(1, 8).Value = "Process Type";

        // Format header
        var headerRange = worksheet.Range(1, 1, 1, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#005B99");
        headerRange.Style.Font.FontColor = XLColor.White;

        // Data rows
        int row = 2;
        foreach (var process in processes)
        {
            worksheet.Cell(row, 1).Value = process.Code;
            worksheet.Cell(row, 2).Value = process.NameEn;
            worksheet.Cell(row, 3).Value = process.NameAr;
            worksheet.Cell(row, 4).Value = process.DescriptionEn;
            worksheet.Cell(row, 5).Value = process.DescriptionAr;
            worksheet.Cell(row, 6).Value = process.ProcessGroup?.NameEn ?? "";
            worksheet.Cell(row, 7).Value = process.Status.ToString();
            worksheet.Cell(row, 8).Value = process.ProcessType.ToString();
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Processes_Export_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// Export all services to Excel
    /// </summary>
    public async Task<IActionResult> ExportServices()
    {
        var services = await _context.Services
            .Include(s => s.OwningUnit)
            .OrderBy(s => s.Code)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Services");

        // Headers
        worksheet.Cell(1, 1).Value = "Code";
        worksheet.Cell(1, 2).Value = "Name (English)";
        worksheet.Cell(1, 3).Value = "Name (Arabic)";
        worksheet.Cell(1, 4).Value = "Description (English)";
        worksheet.Cell(1, 5).Value = "Description (Arabic)";
        worksheet.Cell(1, 6).Value = "Service Type";
        worksheet.Cell(1, 7).Value = "Owning Unit";

        // Format header
        var headerRange = worksheet.Range(1, 1, 1, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#005B99");
        headerRange.Style.Font.FontColor = XLColor.White;

        // Data rows
        int row = 2;
        foreach (var service in services)
        {
            worksheet.Cell(row, 1).Value = service.Code;
            worksheet.Cell(row, 2).Value = service.NameEn;
            worksheet.Cell(row, 3).Value = service.NameAr;
            worksheet.Cell(row, 4).Value = service.DescriptionEn;
            worksheet.Cell(row, 5).Value = service.DescriptionAr;
            worksheet.Cell(row, 6).Value = service.ServiceType.ToString();
            worksheet.Cell(row, 7).Value = service.OwningUnit?.NameEn ?? "";
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Services_Export_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    /// <summary>
    /// Export all risks to Excel
    /// </summary>
    public async Task<IActionResult> ExportRisks()
    {
        var risks = await _context.EnterpriseRisks
            .Include(r => r.Category)
            .OrderBy(r => r.RiskNumber)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Risks");

        // Headers
        worksheet.Cell(1, 1).Value = "Risk Number";
        worksheet.Cell(1, 2).Value = "Name (English)";
        worksheet.Cell(1, 3).Value = "Name (Arabic)";
        worksheet.Cell(1, 4).Value = "Description (English)";
        worksheet.Cell(1, 5).Value = "Description (Arabic)";
        worksheet.Cell(1, 6).Value = "Category";
        worksheet.Cell(1, 7).Value = "Likelihood";
        worksheet.Cell(1, 8).Value = "Impact";
        worksheet.Cell(1, 9).Value = "Inherent Risk Score";

        // Format header
        var headerRange = worksheet.Range(1, 1, 1, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#005B99");
        headerRange.Style.Font.FontColor = XLColor.White;

        // Data rows
        int row = 2;
        foreach (var risk in risks)
        {
            worksheet.Cell(row, 1).Value = risk.RiskNumber;
            worksheet.Cell(row, 2).Value = risk.NameEn;
            worksheet.Cell(row, 3).Value = risk.NameAr;
            worksheet.Cell(row, 4).Value = risk.DescriptionEn;
            worksheet.Cell(row, 5).Value = risk.DescriptionAr;
            worksheet.Cell(row, 6).Value = risk.Category?.NameEn ?? "";
            worksheet.Cell(row, 7).Value = risk.Likelihood;
            worksheet.Cell(row, 8).Value = risk.Impact;
            worksheet.Cell(row, 9).Value = risk.InherentRiskScore;
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        var content = stream.ToArray();

        return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"Risks_Export_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    #endregion
}

