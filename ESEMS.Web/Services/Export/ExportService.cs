using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ESEMS.Web.Data;
using System.Text;

namespace ESEMS.Web.Services.Export;

public class ExportService : IExportService
{
    private readonly ApplicationDbContext _context;
    private readonly Common.HierarchicalCodeService _codeSvc;
    private const string HeaderColor = "#005B99";
    private const string OrgName = "Mohammed Bin Rashid Housing Establishment";

    public ExportService(ApplicationDbContext context, Common.HierarchicalCodeService codeSvc)
    {
        _context = context;
        _codeSvc = codeSvc;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ── Excel Exports ──────────────────────────────────────────────

    public async Task<byte[]> ExportProcessesToExcelAsync()
    {
        var data = await _context.Processes.Where(p => !p.IsDeleted)
            .Include(p => p.ProcessGroup).ThenInclude(pg => pg!.Category)
            .OrderBy(p => p.Code).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Processes");
        var headers = new[] { "Code", "Name (EN)", "Name (AR)", "Category", "Process Group", "Type", "Status", "BPMN", "Created" };
        AddHeaderRow(ws, headers);

        for (int i = 0; i < data.Count; i++)
        {
            var p = data[i];
            ws.Cell(i + 2, 1).Value = p.Code;
            ws.Cell(i + 2, 2).Value = p.NameEn;
            ws.Cell(i + 2, 3).Value = p.NameAr;
            ws.Cell(i + 2, 4).Value = p.ProcessGroup?.Category?.NameEn ?? "";
            ws.Cell(i + 2, 5).Value = p.ProcessGroup?.NameEn ?? "";
            ws.Cell(i + 2, 6).Value = p.ProcessType.ToString();
            ws.Cell(i + 2, 7).Value = p.Status.ToString();
            ws.Cell(i + 2, 8).Value = !string.IsNullOrWhiteSpace(p.BpmnDiagram) ? "Yes" : "No";
            ws.Cell(i + 2, 9).Value = p.CreatedAt.ToString("yyyy-MM-dd");
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public async Task<byte[]> ExportServicesToExcelAsync()
    {
        var data = await _context.Services.Where(s => !s.IsDeleted)
            .Include(s => s.OwningUnit).OrderBy(s => s.Code).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Services");
        AddHeaderRow(ws, new[] { "Code", "Name (EN)", "Name (AR)", "Type", "Channel", "Owning Unit", "Active" });

        for (int i = 0; i < data.Count; i++)
        {
            var s = data[i];
            ws.Cell(i + 2, 1).Value = s.Code;
            ws.Cell(i + 2, 2).Value = s.NameEn;
            ws.Cell(i + 2, 3).Value = s.NameAr;
            ws.Cell(i + 2, 4).Value = s.ServiceType.ToString();
            ws.Cell(i + 2, 5).Value = s.Channel.ToString();
            ws.Cell(i + 2, 6).Value = s.OwningUnit?.NameEn ?? "";
            ws.Cell(i + 2, 7).Value = s.IsActive ? "Yes" : "No";
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public async Task<byte[]> ExportRisksToExcelAsync()
    {
        var data = await _context.EnterpriseRisks.Where(r => !r.IsDeleted)
            .Include(r => r.Category).Include(r => r.OrganizationUnit).OrderBy(r => r.RiskNumber).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Risks");
        AddHeaderRow(ws, new[] { "Risk #", "Name (EN)", "Name (AR)", "Category", "Likelihood", "Impact", "Score", "Level", "Owner" });

        for (int i = 0; i < data.Count; i++)
        {
            var r = data[i];
            ws.Cell(i + 2, 1).Value = r.RiskNumber;
            ws.Cell(i + 2, 2).Value = r.NameEn;
            ws.Cell(i + 2, 3).Value = r.NameAr;
            ws.Cell(i + 2, 4).Value = r.Category?.NameEn ?? "";
            ws.Cell(i + 2, 5).Value = r.Likelihood;
            ws.Cell(i + 2, 6).Value = r.Impact;
            ws.Cell(i + 2, 7).Value = r.InherentRiskScore;
            ws.Cell(i + 2, 8).Value = r.RiskLevel.ToString();
            ws.Cell(i + 2, 9).Value = r.OrganizationUnit?.NameEn ?? "";
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public async Task<byte[]> ExportIncidentsToExcelAsync()
    {
        var data = await _context.Incidents.Where(i => !i.IsDeleted)
            .Include(i => i.Service).OrderByDescending(i => i.ReportedAt).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Incidents");
        AddHeaderRow(ws, new[] { "Incident #", "Name (EN)", "Priority", "Status", "Service", "Reported", "Resolved" });

        for (int i = 0; i < data.Count; i++)
        {
            var inc = data[i];
            ws.Cell(i + 2, 1).Value = inc.IncidentNumber;
            ws.Cell(i + 2, 2).Value = inc.NameEn;
            ws.Cell(i + 2, 3).Value = inc.Priority.ToString();
            ws.Cell(i + 2, 4).Value = inc.Status.ToString();
            ws.Cell(i + 2, 5).Value = inc.Service?.NameEn ?? "";
            ws.Cell(i + 2, 6).Value = inc.ReportedAt.ToString("yyyy-MM-dd HH:mm");
            ws.Cell(i + 2, 7).Value = inc.ResolvedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public async Task<byte[]> ExportImprovementsToExcelAsync()
    {
        var data = await _context.ImprovementInitiatives.Where(i => !i.IsDeleted)
            .OrderBy(i => i.Code).ToListAsync();

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Improvements");
        AddHeaderRow(ws, ImprovementImportColumns);

        for (int i = 0; i < data.Count; i++)
        {
            var imp = data[i];
            ws.Cell(i + 2, 1).Value = imp.Code;
            ws.Cell(i + 2, 2).Value = imp.TitleEn;
            ws.Cell(i + 2, 3).Value = imp.TitleAr;
            ws.Cell(i + 2, 4).Value = imp.Status.ToString();
            ws.Cell(i + 2, 5).Value = imp.Priority;
            ws.Cell(i + 2, 6).Value = imp.Quadrant.ToString();
            ws.Cell(i + 2, 7).Value = imp.ProgressPercentage;
            ws.Cell(i + 2, 8).Value = imp.ImpactScore;
            ws.Cell(i + 2, 9).Value = imp.EffortScore;
            ws.Cell(i + 2, 10).Value = imp.EstimatedCostSavings;
            ws.Cell(i + 2, 11).Value = imp.EstimatedTimeSavings;
            ws.Cell(i + 2, 12).Value = imp.OwningUnitId?.ToString() ?? string.Empty;
            ws.Cell(i + 2, 13).Value = imp.StrategicObjectiveId ?? string.Empty;
            ws.Cell(i + 2, 14).Value = imp.CreatedAt.ToString("yyyy-MM-dd");
        }

        ws.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    // ── Audit #20: Improvements bulk import ───────────────────────────

    private static readonly string[] ImprovementImportColumns = new[]
    {
        "Code", "Title (EN)", "Title (AR)", "Status", "Priority", "Quadrant",
        "Progress %", "Impact (1-10)", "Effort (1-10)",
        "Est. Cost Savings (AED)", "Est. Time Savings (hrs)",
        "Owning Unit Id", "Strategic Objective Id", "Created"
    };

    public byte[] BuildImprovementsImportTemplate()
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Improvements");
        AddHeaderRow(ws, ImprovementImportColumns);

        // Sample row so the user knows the format. Status / Quadrant must
        // match the enum names exactly — we list them in a Notes sheet.
        ws.Cell(2, 1).Value = "(auto)";
        ws.Cell(2, 2).Value = "Sample initiative — replace me";
        ws.Cell(2, 3).Value = "مبادرة تجريبية — استبدلني";
        ws.Cell(2, 4).Value = "Proposed";
        ws.Cell(2, 5).Value = 3;
        ws.Cell(2, 6).Value = "QuickWins";
        ws.Cell(2, 7).Value = 0;
        ws.Cell(2, 8).Value = 7;
        ws.Cell(2, 9).Value = 4;
        ws.Cell(2, 10).Value = 50000;
        ws.Cell(2, 11).Value = 120;

        var notes = wb.Worksheets.Add("Notes");
        notes.Cell(1, 1).Value = "Allowed Status values:";
        notes.Cell(2, 1).Value = string.Join(", ", Enum.GetNames<Models.Enums.ImprovementStatus>());
        notes.Cell(4, 1).Value = "Allowed Quadrant values:";
        notes.Cell(5, 1).Value = "QuickWins, MajorProjects, FillIns, ThanklessTasks";
        notes.Cell(7, 1).Value = "Code: leave blank or '(auto)' to let HierarchicalCodeService generate INI-{NNN}.";
        notes.Cell(8, 1).Value = "Owning Unit Id / Strategic Objective Id: paste the entity Id (or leave blank).";

        ws.Columns().AdjustToContents();
        notes.Columns().AdjustToContents();
        return WorkbookToBytes(wb);
    }

    public async Task<ImportImprovementsResult> ImportImprovementsFromExcelAsync(
        Stream xlsxStream, string? createdById, CancellationToken ct = default)
    {
        var result = new ImportImprovementsResult();

        XLWorkbook wb;
        try { wb = new XLWorkbook(xlsxStream); }
        catch (Exception ex)
        {
            result.Errors.Add(new ImportImprovementsRowError { Row = 0, Message = $"File is not a readable XLSX: {ex.Message}" });
            return result;
        }

        using (wb)
        {
            var ws = wb.Worksheets.FirstOrDefault();
            if (ws is null)
            {
                result.Errors.Add(new ImportImprovementsRowError { Row = 0, Message = "Workbook has no sheets." });
                return result;
            }

            // Verify the header row matches the template — fail fast on column drift.
            for (var col = 0; col < ImprovementImportColumns.Length; col++)
            {
                var actual = ws.Cell(1, col + 1).GetString().Trim();
                if (!string.Equals(actual, ImprovementImportColumns[col], StringComparison.OrdinalIgnoreCase))
                {
                    result.Errors.Add(new ImportImprovementsRowError
                    {
                        Row = 1,
                        Message = $"Column {col + 1} header mismatch — expected '{ImprovementImportColumns[col]}', got '{actual}'. Re-download the template."
                    });
                    return result;
                }
            }

            var lastRow = ws.LastRowUsed();
            if (lastRow is null) return result;

            // NextInitiativeCodeAsync returns max+1; assigning the same value
            // to every row would violate the unique index. Seed once, then
            // increment locally for each row in the batch.
            var seedCode = await _codeSvc.NextInitiativeCodeAsync(ct);
            var seedNum = int.Parse(seedCode.AsSpan("INI-".Length));
            var nextNum = seedNum;

            for (var rowIdx = 2; rowIdx <= lastRow.RowNumber(); rowIdx++)
            {
                if (ct.IsCancellationRequested) break;
                result.TotalRows++;

                try
                {
                    var titleEn = ws.Cell(rowIdx, 2).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(titleEn))
                    {
                        result.Skipped++;
                        continue; // empty row — silently skip
                    }

                    var statusStr = ws.Cell(rowIdx, 4).GetString().Trim();
                    if (string.IsNullOrWhiteSpace(statusStr)) statusStr = "Proposed";
                    if (!Enum.TryParse<Models.Enums.ImprovementStatus>(statusStr, ignoreCase: true, out var status))
                        throw new InvalidOperationException($"Unknown Status '{statusStr}'");

                    var quadStr = ws.Cell(rowIdx, 6).GetString().Trim();
                    var quad = Models.Enums.ImprovementQuadrant.FillIns;
                    if (!string.IsNullOrWhiteSpace(quadStr) && !Enum.TryParse(quadStr, ignoreCase: true, out quad))
                        throw new InvalidOperationException($"Unknown Quadrant '{quadStr}'");

                    var initiative = new Models.Improvement.ImprovementInitiative
                    {
                        Id = Guid.NewGuid().ToString(),
                        Code = $"INI-{nextNum++:000}",
                        TitleEn = titleEn,
                        TitleAr = ws.Cell(rowIdx, 3).GetString().Trim(),
                        NameEn = titleEn,
                        NameAr = ws.Cell(rowIdx, 3).GetString().Trim(),
                        Status = status,
                        Priority = (int?)ws.Cell(rowIdx, 5).GetValue<double?>() ?? 3,
                        Quadrant = quad,
                        ProgressPercentage = (int?)ws.Cell(rowIdx, 7).GetValue<double?>() ?? 0,
                        ImpactScore = (int?)ws.Cell(rowIdx, 8).GetValue<double?>() ?? 5,
                        EffortScore = (int?)ws.Cell(rowIdx, 9).GetValue<double?>() ?? 5,
                        EstimatedCostSavings = ws.Cell(rowIdx, 10).GetValue<decimal?>(),
                        EstimatedTimeSavings = ws.Cell(rowIdx, 11).GetValue<decimal?>(),
                        OwningUnitId = int.TryParse(ws.Cell(rowIdx, 12).GetString()?.Trim(), out var parsedUnitId) ? parsedUnitId : (int?)null,
                        StrategicObjectiveId = ws.Cell(rowIdx, 13).GetString()?.Trim() is { Length: > 0 } o ? o : null,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow,
                        CreatedById = createdById
                    };

                    _context.ImprovementInitiatives.Add(initiative);
                    result.Inserted++;
                }
                catch (Exception ex)
                {
                    result.Errors.Add(new ImportImprovementsRowError { Row = rowIdx, Message = ex.Message });
                    result.Skipped++;
                }
            }

            await _context.SaveChangesAsync(ct);
        }

        return result;
    }

    // ── PDF Exports ──────────────────────────────────────────────
    // F-025: removed ExportProcesses/Services/RisksToPdfAsync. They were never
    // called (the live PDF export builds QuestPDF documents directly in
    // Api/ExportController, which carries the F-002 Arabic font) and all three
    // delegated to a shared GeneratePdf helper that built a table without a
    // ColumnsDefinition — guaranteed to throw at compose if ever invoked.

    // ── CSV Export ──────────────────────────────────────────────

    public async Task<byte[]> ExportToCsvAsync(string entityType)
    {
        var sb = new StringBuilder();
        switch (entityType.ToLower())
        {
            case "processes":
                var processes = await _context.Processes.Where(p => !p.IsDeleted).OrderBy(p => p.Code).ToListAsync();
                sb.AppendLine("Code,NameEN,NameAR,Status,Type");
                foreach (var p in processes) sb.AppendLine($"{Csv(p.Code)},{Csv(p.NameEn)},{Csv(p.NameAr)},{Csv(p.Status.ToString())},{Csv(p.ProcessType.ToString())}");
                break;
            case "services":
                var services = await _context.Services.Where(s => !s.IsDeleted).OrderBy(s => s.Code).ToListAsync();
                sb.AppendLine("Code,NameEN,NameAR,Type,Channel");
                foreach (var s in services) sb.AppendLine($"{Csv(s.Code)},{Csv(s.NameEn)},{Csv(s.NameAr)},{Csv(s.ServiceType.ToString())},{Csv(s.Channel.ToString())}");
                break;
            case "risks":
                var risks = await _context.EnterpriseRisks.Where(r => !r.IsDeleted).OrderBy(r => r.RiskNumber).ToListAsync();
                sb.AppendLine("RiskNumber,NameEN,Likelihood,Impact,Score,Level");
                foreach (var r in risks) sb.AppendLine($"{Csv(r.RiskNumber)},{Csv(r.NameEn)},{r.Likelihood},{r.Impact},{r.InherentRiskScore},{Csv(r.RiskLevel.ToString())}");
                break;
            default:
                sb.AppendLine("Unsupported entity type");
                break;
        }
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
    }

    // ── Helpers ──────────────────────────────────────────────

    private static void AddHeaderRow(IXLWorksheet ws, string[] headers)
    {
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml(HeaderColor);
            cell.Style.Font.FontColor = XLColor.White;
        }
    }

    private static byte[] WorkbookToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    /// <summary>
    /// F-017: CSV-injection-safe cell. Doubles embedded quotes (RFC-4180) and
    /// prefixes a leading =,+,-,@ (or control char) with an apostrophe so
    /// Excel/Sheets don't evaluate the value as a formula.
    /// </summary>
    private static string Csv(string? value)
    {
        var v = value ?? "";
        if (v.Length > 0 && (v[0] is '=' or '+' or '-' or '@' or '\t' or '\r'))
            v = "'" + v;
        return "\"" + v.Replace("\"", "\"\"") + "\"";
    }
}
