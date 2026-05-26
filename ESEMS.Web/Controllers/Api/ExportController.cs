using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Security;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ESEMS.Web.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize(Policy = AppPolicies.CanView)]
// RBAC-007 — defensive: all current endpoints are [HttpGet] (CSRF-immune)
// but the next state-changing export operation added here (schedule export,
// queue export-to-database) would silently inherit the lack of anti-forgery.
// AutoValidate now so future writes can't bypass.
[AutoValidateAntiforgeryToken]
public class ExportController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ExportController(ApplicationDbContext context)
    {
        _context = context;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    #region Processes Export

    [HttpGet("processes/excel")]
    public async Task<IActionResult> ExportProcessesToExcel()
    {
        var processes = await _context.Processes
            .Where(p => !p.IsDeleted)
            .Include(p => p.ProcessGroup)
            .ThenInclude(pg => pg!.Category)
            .OrderBy(p => p.Code)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Processes");

        // Header row
        worksheet.Cell(1, 1).Value = "Code";
        worksheet.Cell(1, 2).Value = "Name (EN)";
        worksheet.Cell(1, 3).Value = "Name (AR)";
        worksheet.Cell(1, 4).Value = "Category";
        worksheet.Cell(1, 5).Value = "Process Group";
        worksheet.Cell(1, 6).Value = "Status";
        worksheet.Cell(1, 7).Value = "Has BPMN";
        worksheet.Cell(1, 8).Value = "Created At";

        // Style header
        var headerRange = worksheet.Range(1, 1, 1, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4B73");
        headerRange.Style.Font.FontColor = XLColor.White;

        // Data rows
        int row = 2;
        foreach (var process in processes)
        {
            worksheet.Cell(row, 1).Value = process.Code;
            worksheet.Cell(row, 2).Value = process.NameEn;
            worksheet.Cell(row, 3).Value = process.NameAr;
            worksheet.Cell(row, 4).Value = process.ProcessGroup?.Category?.NameEn ?? "";
            worksheet.Cell(row, 5).Value = process.ProcessGroup?.NameEn ?? "";
            worksheet.Cell(row, 6).Value = process.Status.ToString();
            worksheet.Cell(row, 7).Value = !string.IsNullOrEmpty(process.BpmnDiagram) ? "Yes" : "No";
            worksheet.Cell(row, 8).Value = process.CreatedAt.ToString("yyyy-MM-dd");
            row++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", 
            $"Processes_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("processes/pdf")]
    public async Task<IActionResult> ExportProcessesToPdf()
    {
        var processes = await _context.Processes
            .Where(p => !p.IsDeleted)
            .Include(p => p.ProcessGroup)
            .ThenInclude(pg => pg!.Category)
            .OrderBy(p => p.Code)
            .ToListAsync();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(ESEMS.Web.Services.Export.PdfFonts.Family));

                page.Header().Element(c => ComposeHeader(c, "Processes Report"));
                page.Content().Element(c => ComposeProcessesContent(c, processes));
                page.Footer().Element(ComposeFooter);
            });
        });

        var pdf = document.GeneratePdf();
        return File(pdf, "application/pdf", $"Processes_{DateTime.Now:yyyyMMdd}.pdf");
    }

    #endregion

    #region Services Export

    [HttpGet("services/excel")]
    public async Task<IActionResult> ExportServicesToExcel()
    {
        var services = await _context.Services
            .Where(s => !s.IsDeleted)
            .Include(s => s.OwningUnit)
            .OrderBy(s => s.Code)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Services");

        // Header row
        worksheet.Cell(1, 1).Value = "Code";
        worksheet.Cell(1, 2).Value = "Name (EN)";
        worksheet.Cell(1, 3).Value = "Name (AR)";
        worksheet.Cell(1, 4).Value = "Type";
        worksheet.Cell(1, 5).Value = "Channel";
        worksheet.Cell(1, 6).Value = "Owning Unit";
        worksheet.Cell(1, 7).Value = "SLA (Days)";
        worksheet.Cell(1, 8).Value = "Active";

        // Style header
        var headerRange = worksheet.Range(1, 1, 1, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4B73");
        headerRange.Style.Font.FontColor = XLColor.White;

        // Data rows
        int row = 2;
        foreach (var service in services)
        {
            worksheet.Cell(row, 1).Value = service.Code;
            worksheet.Cell(row, 2).Value = service.NameEn;
            worksheet.Cell(row, 3).Value = service.NameAr;
            worksheet.Cell(row, 4).Value = service.ServiceType.ToString();
            worksheet.Cell(row, 5).Value = service.Channel.ToString();
            worksheet.Cell(row, 6).Value = service.OwningUnit?.NameEn ?? "";
            worksheet.Cell(row, 7).Value = service.SLADays;
            worksheet.Cell(row, 8).Value = service.IsActive ? "Yes" : "No";
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Services_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("services/pdf")]
    public async Task<IActionResult> ExportServicesToPdf()
    {
        var services = await _context.Services
            .Where(s => !s.IsDeleted)
            .Include(s => s.OwningUnit)
            .OrderBy(s => s.Code)
            .ToListAsync();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(ESEMS.Web.Services.Export.PdfFonts.Family));

                page.Header().Element(c => ComposeHeader(c, "Services Report"));
                page.Content().Element(c => ComposeServicesContent(c, services));
                page.Footer().Element(ComposeFooter);
            });
        });

        var pdf = document.GeneratePdf();
        return File(pdf, "application/pdf", $"Services_{DateTime.Now:yyyyMMdd}.pdf");
    }

    #endregion

    #region Enterprise Risks Export

    [HttpGet("risks/excel")]
    public async Task<IActionResult> ExportRisksToExcel()
    {
        var risks = await _context.EnterpriseRisks
            .Where(r => !r.IsDeleted)
            .Include(r => r.Category)
            .OrderByDescending(r => r.InherentRiskScore)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Risks");

        // Header row
        worksheet.Cell(1, 1).Value = "Risk Number";
        worksheet.Cell(1, 2).Value = "Name (EN)";
        worksheet.Cell(1, 3).Value = "Category";
        worksheet.Cell(1, 4).Value = "Likelihood";
        worksheet.Cell(1, 5).Value = "Impact";
        worksheet.Cell(1, 6).Value = "Inherent Score";
        worksheet.Cell(1, 7).Value = "Residual Score";
        worksheet.Cell(1, 8).Value = "Risk Level";
        worksheet.Cell(1, 9).Value = "Owner";

        var headerRange = worksheet.Range(1, 1, 1, 9);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4B73");
        headerRange.Style.Font.FontColor = XLColor.White;

        int row = 2;
        foreach (var risk in risks)
        {
            worksheet.Cell(row, 1).Value = risk.RiskNumber;
            worksheet.Cell(row, 2).Value = risk.NameEn;
            worksheet.Cell(row, 3).Value = risk.Category?.NameEn ?? "";
            worksheet.Cell(row, 4).Value = risk.Likelihood;
            worksheet.Cell(row, 5).Value = risk.Impact;
            worksheet.Cell(row, 6).Value = risk.InherentRiskScore;
            worksheet.Cell(row, 7).Value = risk.ResidualRiskScore;
            worksheet.Cell(row, 8).Value = risk.RiskLevel.ToString();
            worksheet.Cell(row, 9).Value = risk.Owner?.Email ?? "";
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"EnterpriseRisks_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    [HttpGet("risks/pdf")]
    public async Task<IActionResult> ExportRisksToPdf()
    {
        var risks = await _context.EnterpriseRisks
            .Where(r => !r.IsDeleted)
            .Include(r => r.Category)
            .OrderByDescending(r => r.InherentRiskScore)
            .ToListAsync();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(30);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily(ESEMS.Web.Services.Export.PdfFonts.Family));

                page.Header().Element(c => ComposeHeader(c, "Enterprise Risks Report"));
                page.Content().Element(c => ComposeRisksContent(c, risks));
                page.Footer().Element(ComposeFooter);
            });
        });

        var pdf = document.GeneratePdf();
        return File(pdf, "application/pdf", $"EnterpriseRisks_{DateTime.Now:yyyyMMdd}.pdf");
    }

    #endregion

    #region Incidents Export

    [HttpGet("incidents/excel")]
    public async Task<IActionResult> ExportIncidentsToExcel()
    {
        var incidents = await _context.Incidents
            .Where(i => !i.IsDeleted)
            .Include(i => i.Process)
            .Include(i => i.Service)
            .OrderByDescending(i => i.ReportedAt)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Incidents");

        worksheet.Cell(1, 1).Value = "Incident Number";
        worksheet.Cell(1, 2).Value = "Name (EN)";
        worksheet.Cell(1, 3).Value = "Priority";
        worksheet.Cell(1, 4).Value = "Status";
        worksheet.Cell(1, 5).Value = "Service";
        worksheet.Cell(1, 6).Value = "Reported At";
        worksheet.Cell(1, 7).Value = "Resolved At";

        var headerRange = worksheet.Range(1, 1, 1, 7);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4B73");
        headerRange.Style.Font.FontColor = XLColor.White;

        int row = 2;
        foreach (var incident in incidents)
        {
            worksheet.Cell(row, 1).Value = incident.IncidentNumber;
            worksheet.Cell(row, 2).Value = incident.NameEn;
            worksheet.Cell(row, 3).Value = incident.Priority.ToString();
            worksheet.Cell(row, 4).Value = incident.Status.ToString();
            worksheet.Cell(row, 5).Value = incident.Service?.NameEn ?? "";
            worksheet.Cell(row, 6).Value = incident.ReportedAt.ToString("yyyy-MM-dd HH:mm");
            worksheet.Cell(row, 7).Value = incident.ResolvedAt?.ToString("yyyy-MM-dd HH:mm") ?? "";
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Incidents_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    #endregion

    #region Improvements Export

    [HttpGet("improvements/excel")]
    public async Task<IActionResult> ExportImprovementsToExcel()
    {
        var improvements = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted)
            .Include(i => i.Process)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Improvements");

        worksheet.Cell(1, 1).Value = "Code";
        worksheet.Cell(1, 2).Value = "Title (EN)";
        worksheet.Cell(1, 3).Value = "Status";
        worksheet.Cell(1, 4).Value = "Priority";
        worksheet.Cell(1, 5).Value = "Source";
        worksheet.Cell(1, 6).Value = "Process";
        worksheet.Cell(1, 7).Value = "Progress %";
        worksheet.Cell(1, 8).Value = "Created At";

        var headerRange = worksheet.Range(1, 1, 1, 8);
        headerRange.Style.Font.Bold = true;
        headerRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#1B4B73");
        headerRange.Style.Font.FontColor = XLColor.White;

        int row = 2;
        foreach (var improvement in improvements)
        {
            worksheet.Cell(row, 1).Value = improvement.Code;
            worksheet.Cell(row, 2).Value = improvement.TitleEn;
            worksheet.Cell(row, 3).Value = improvement.Status.ToString();
            worksheet.Cell(row, 4).Value = improvement.Priority.ToString();
            worksheet.Cell(row, 5).Value = improvement.Source.ToString();
            worksheet.Cell(row, 6).Value = improvement.Process?.NameEn ?? "";
            worksheet.Cell(row, 7).Value = improvement.ProgressPercentage;
            worksheet.Cell(row, 8).Value = improvement.CreatedAt.ToString("yyyy-MM-dd");
            row++;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Improvements_{DateTime.Now:yyyyMMdd}.xlsx");
    }

    #endregion

    #region PDF Helper Methods

    private void ComposeHeader(IContainer container, string title)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(column =>
            {
                column.Item().Text("MBRHE - Mohammed Bin Rashid Housing Establishment")
                    .FontSize(14).Bold().FontColor(Colors.Blue.Darken3);
                column.Item().Text(title)
                    .FontSize(12).SemiBold();
                column.Item().Text($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm}")
                    .FontSize(9).FontColor(Colors.Grey.Medium);
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.AlignCenter().Text(x =>
        {
            x.Span("Page ");
            x.CurrentPageNumber();
            x.Span(" of ");
            x.TotalPages();
        });
    }

    private void ComposeProcessesContent(IContainer container, List<Models.APQC.Process> processes)
    {
        container.PaddingVertical(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(60);  // Code
                columns.RelativeColumn(2);   // Name EN
                columns.RelativeColumn(2);   // Name AR
                columns.RelativeColumn(1.5f); // Category
                columns.RelativeColumn(1.5f); // Process Group
                columns.ConstantColumn(60);  // Status
                columns.ConstantColumn(50);  // BPMN
            });

            // Header
            table.Header(header =>
            {
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Code").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Name (EN)").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Name (AR)").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Category").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Process Group").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Status").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("BPMN").FontColor(Colors.White).Bold();
            });

            // Data rows
            foreach (var process in processes)
            {
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(process.Code);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(process.NameEn);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(process.NameAr);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(process.ProcessGroup?.Category?.NameEn ?? "");
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(process.ProcessGroup?.NameEn ?? "");
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(process.Status.ToString());
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(!string.IsNullOrEmpty(process.BpmnDiagram) ? "Yes" : "No");
            }
        });
    }

    private void ComposeServicesContent(IContainer container, List<Models.Services.Service> services)
    {
        container.PaddingVertical(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(60);
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.ConstantColumn(80);
                columns.ConstantColumn(80);
                columns.RelativeColumn(1.5f);
                columns.ConstantColumn(50);
            });

            table.Header(header =>
            {
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Code").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Name (EN)").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Name (AR)").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Type").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Channel").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Owning Unit").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("SLA").FontColor(Colors.White).Bold();
            });

            foreach (var service in services)
            {
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(service.Code);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(service.NameEn);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(service.NameAr);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(service.ServiceType.ToString());
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(service.Channel.ToString());
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(service.OwningUnit?.NameEn ?? "");
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(service.SLADays?.ToString() ?? "");
            }
        });
    }

    private void ComposeRisksContent(IContainer container, List<Models.RiskManagement.EnterpriseRisk> risks)
    {
        container.PaddingVertical(10).Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(60);
                columns.RelativeColumn(2);
                columns.RelativeColumn(1.5f);
                columns.ConstantColumn(50);
                columns.ConstantColumn(50);
                columns.ConstantColumn(60);
                columns.ConstantColumn(60);
                columns.ConstantColumn(70);
            });

            table.Header(header =>
            {
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Risk #").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Name").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Category").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("L").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("I").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Inherent").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Residual").FontColor(Colors.White).Bold();
                header.Cell().Background(Colors.Blue.Darken3).Padding(5).Text("Level").FontColor(Colors.White).Bold();
            });

            foreach (var risk in risks)
            {
                var bgColor = risk.InherentRiskScore >= 15 ? Colors.Red.Lighten4 :
                              risk.InherentRiskScore >= 10 ? Colors.Orange.Lighten4 :
                              risk.InherentRiskScore >= 5 ? Colors.Yellow.Lighten4 : Colors.Green.Lighten4;

                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(bgColor).Padding(5).Text(risk.RiskNumber);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(bgColor).Padding(5).Text(risk.NameEn);
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(bgColor).Padding(5).Text(risk.Category?.NameEn ?? "");
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(bgColor).Padding(5).Text(risk.Likelihood.ToString());
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(bgColor).Padding(5).Text(risk.Impact.ToString());
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(bgColor).Padding(5).Text(risk.InherentRiskScore.ToString());
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(bgColor).Padding(5).Text(risk.ResidualRiskScore?.ToString() ?? "");
                table.Cell().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).Background(bgColor).Padding(5).Text(risk.RiskLevel.ToString());
            }
        });
    }

    #endregion
}

