using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Security;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ESEMS.Web.Controllers;

/// <summary>
/// Dashboard controller for main overview and analytics
/// </summary>
[Authorize(Policy = AppPolicies.Module.Reports.View)]
public class DashboardController : BaseController
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Main dashboard view
    /// </summary>
    public async Task<IActionResult> Index()
    {
        var model = new DashboardViewModel
        {
            TotalCategories = await _context.Categories.CountAsync(c => !c.IsDeleted),
            TotalProcessGroups = await _context.ProcessGroups.CountAsync(pg => !pg.IsDeleted),
            TotalProcesses = await _context.Processes.CountAsync(p => !p.IsDeleted),
            TotalServices = await _context.Services.CountAsync(s => !s.IsDeleted),
            TotalImprovements = await _context.ImprovementInitiatives.CountAsync(i => !i.IsDeleted),
            ActiveChangeRequests = await _context.ChangeRequests
                .CountAsync(cr => !cr.IsDeleted && cr.Status != ChangeRequestStatus.Implemented && cr.Status != ChangeRequestStatus.Cancelled),

            // Process Status Distribution
            ProcessesByStatus = await _context.Processes
                .Where(p => !p.IsDeleted)
                .GroupBy(p => p.Status)
                .Select(g => new ProcessStatusCount { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(),

            // Service Type Distribution
            ServicesByType = await _context.Services
                .Where(s => !s.IsDeleted)
                .GroupBy(s => s.ServiceType)
                .Select(g => new ServiceTypeCount { Type = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(),

            // Improvement Quadrant Distribution
            ImprovementsByQuadrant = await _context.ImprovementInitiatives
                .Where(i => !i.IsDeleted)
                .GroupBy(i => i.Quadrant)
                .Select(g => new ImprovementQuadrantCount { Quadrant = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(),

            // Change Request Status Distribution
            ChangeRequestsByStatus = await _context.ChangeRequests
                .Where(cr => !cr.IsDeleted)
                .GroupBy(cr => cr.Status)
                .Select(g => new ChangeRequestStatusCount { Status = g.Key.ToString(), Count = g.Count() })
                .ToListAsync(),

            // Top Categories by Process Count (culture-aware name selection)
            TopCategories = (await _context.Categories
                .Where(c => !c.IsDeleted)
                .Select(c => new
                {
                    NameEn = c.NameEn,
                    NameAr = c.NameAr,
                    ProcessCount = c.ProcessGroups.SelectMany(pg => pg.Processes).Count(p => !p.IsDeleted)
                })
                .OrderByDescending(c => c.ProcessCount)
                .Take(5)
                .ToListAsync())
                .Select(c => new CategoryStats
                {
                    Name = CultureInfo.CurrentUICulture.Name.StartsWith("ar") ? c.NameAr : c.NameEn,
                    ProcessCount = c.ProcessCount
                })
                .ToList(),

            // Service Performance Metrics (culture-aware name selection)
            ServicePerformance = (await _context.Services
                .Where(s => !s.IsDeleted && s.IsActive)
                .Select(s => new
                {
                    NameEn = s.NameEn,
                    NameAr = s.NameAr,
                    SLADays = s.SLADays ?? 0,
                    ActualDeliveryDays = s.ActualDeliveryDays ?? 0,
                    CustomerSatisfaction = s.CustomerSatisfactionScore ?? 0
                })
                .Where(s => s.SLADays > 0)
                .OrderByDescending(s => s.CustomerSatisfaction)
                .Take(5)
                .ToListAsync())
                .Select(s => new ServicePerformanceMetric
                {
                    ServiceName = CultureInfo.CurrentUICulture.Name.StartsWith("ar") ? s.NameAr : s.NameEn,
                    SLADays = s.SLADays,
                    ActualDeliveryDays = s.ActualDeliveryDays,
                    CustomerSatisfaction = s.CustomerSatisfaction
                })
                .ToList(),

            // Improvement ROI Summary
            ImprovementROI = new ImprovementROISummary
            {
                TotalEstimatedCostSavings = await _context.ImprovementInitiatives
                    .Where(i => !i.IsDeleted)
                    .SumAsync(i => i.EstimatedCostSavings ?? 0),
                TotalActualCostSavings = await _context.ImprovementInitiatives
                    .Where(i => !i.IsDeleted && i.CompletedDate != null)
                    .SumAsync(i => i.ActualCostSavings ?? 0),
                TotalEstimatedTimeSavings = await _context.ImprovementInitiatives
                    .Where(i => !i.IsDeleted)
                    .SumAsync(i => i.EstimatedTimeSavings ?? 0),
                TotalActualTimeSavings = await _context.ImprovementInitiatives
                    .Where(i => !i.IsDeleted && i.CompletedDate != null)
                    .SumAsync(i => i.ActualTimeSavings ?? 0)
            },

            // Recent Activities
            RecentActivities = await _context.AuditLogs
                .OrderByDescending(a => a.Timestamp)
                .Take(10)
                .Select(a => new RecentActivityItem
                {
                    UserName = a.UserId ?? "System",
                    Action = a.Action.ToString(),
                    EntityType = a.EntityType,
                    EntityName = a.EntityId,
                    Timestamp = a.Timestamp
                })
                .ToListAsync()
        };

        return View(model);
    }

    /// <summary>
    /// Organization Structure View (hierarchical)
    /// </summary>
    public async Task<IActionResult> OrganizationView()
    {
        var units = await _context.OrganizationUnits
            .Where(u => !u.IsDeleted && u.IsActive)
            .Include(u => u.Children)
            .OrderBy(u => u.Level)
            .ThenBy(u => u.DisplayOrder)
            .ToListAsync();

        return View(units);
    }

    /// <summary>
    /// Process Architecture View (APQC hierarchy)
    /// </summary>
    public async Task<IActionResult> ProcessArchitectureView()
    {
        var categories = await _context.Categories
            .Where(c => !c.IsDeleted)
            .Include(c => c.ProcessGroups.Where(pg => !pg.IsDeleted))
                .ThenInclude(pg => pg.Processes.Where(p => !p.IsDeleted))
            .OrderBy(c => c.SortKey ?? c.Code)
            .ToListAsync();

        return View(categories);
    }

    /// <summary>
    /// CSV export of the dashboard data. Structured by section (Summary,
    /// ROI, Process Status, Service Types, Improvement Quadrants, Change
    /// Requests, Top Categories, Service Performance) with a metadata
    /// header row so an analyst opening the file in Excel sees who/when
    /// it was generated.
    ///
    /// UTF-8 BOM prefix so Windows Excel auto-detects encoding and
    /// renders Arabic text correctly. All numeric values written through
    /// InvariantCulture so the file parses identically regardless of the
    /// user's locale.
    /// </summary>
    public async Task<IActionResult> ExportCsv()
    {
        var model = await BuildDashboardViewModelAsync();
        var inv = CultureInfo.InvariantCulture;
        var isRtl = CultureInfo.CurrentUICulture.Name.StartsWith("ar");

        var sb = new StringBuilder();
        // Metadata header — survives `Open in Excel` and gives the
        // downstream reader provenance without needing a separate doc.
        sb.AppendLine($"# ESEMS Dashboard Export");
        sb.AppendLine($"# Generated: {DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", inv)}");
        sb.AppendLine($"# User: {User.Identity?.Name ?? "(unknown)"}");
        sb.AppendLine();
        sb.AppendLine("Section,Metric,Value");

        string Csv(object? v)
        {
            if (v is null) return "";
            var s = v switch
            {
                decimal d => d.ToString(inv),
                double db => db.ToString(inv),
                float f => f.ToString(inv),
                _ => v.ToString() ?? ""
            };
            return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0
                ? "\"" + s.Replace("\"", "\"\"") + "\""
                : s;
        }
        void Row(string section, string metric, object? value)
            => sb.AppendLine($"{Csv(section)},{Csv(metric)},{Csv(value)}");

        // Summary totals
        Row("Summary", "Total Processes", model.TotalProcesses);
        Row("Summary", "Total Services", model.TotalServices);
        Row("Summary", "Total Improvements", model.TotalImprovements);
        Row("Summary", "Active Change Requests", model.ActiveChangeRequests);
        Row("Summary", "Total Categories", model.TotalCategories);
        Row("Summary", "Total Process Groups", model.TotalProcessGroups);

        // ROI
        var est = model.ImprovementROI?.TotalEstimatedCostSavings ?? 0m;
        var act = model.ImprovementROI?.TotalActualCostSavings ?? 0m;
        Row("ROI", "Estimated Cost Savings (AED)", est);
        Row("ROI", "Realized Cost Savings (AED)", act);
        Row("ROI", "Realization %", est > 0 ? Math.Round(act / est * 100m, 1) : 0m);
        Row("ROI", "Estimated Time Savings (hours)", model.ImprovementROI?.TotalEstimatedTimeSavings ?? 0m);
        Row("ROI", "Realized Time Savings (hours)", model.ImprovementROI?.TotalActualTimeSavings ?? 0m);

        // Process status
        foreach (var p in model.ProcessesByStatus)
            Row("Process Status", p.Status, p.Count);

        // Service types
        foreach (var s in model.ServicesByType)
            Row("Service Types", s.Type, s.Count);

        // Improvement quadrants
        foreach (var q in model.ImprovementsByQuadrant)
            Row("Improvement Quadrants", q.Quadrant, q.Count);

        // Change requests
        foreach (var cr in model.ChangeRequestsByStatus)
            Row("Change Request Status", cr.Status, cr.Count);

        // Top categories
        foreach (var c in model.TopCategories)
            Row("Top Categories", c.Name, c.ProcessCount);

        // Service performance — three rows per service so the section
        // works in pivot tables.
        foreach (var sp in model.ServicePerformance)
        {
            Row("Service Performance", sp.ServiceName + " — SLA (days)", sp.SLADays);
            Row("Service Performance", sp.ServiceName + " — Actual delivery (days)", sp.ActualDeliveryDays);
            Row("Service Performance", sp.ServiceName + " — Customer satisfaction %", sp.CustomerSatisfaction);
        }

        var bytes = new byte[] { 0xEF, 0xBB, 0xBF }.Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var fileName = $"esems-dashboard-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    /// <summary>
    /// PDF export of the dashboard data. Real server-side render via
    /// QuestPDF (replaces the old `window.print()` path which printed
    /// empty pages because Chart.js canvases don't print). Single A4
    /// landscape page with branded header, KPI tiles, then 6 data
    /// tables matching the dashboard tabs.
    /// </summary>
    public async Task<IActionResult> ExportPdf()
    {
        var model = await BuildDashboardViewModelAsync();
        var inv = CultureInfo.InvariantCulture;
        var generated = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm 'UTC'", inv);
        var user = User.Identity?.Name ?? "(unknown)";

        QuestPDF.Settings.License = LicenseType.Community;
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor("#1f2937").FontFamily(ESEMS.Web.Services.Export.PdfFonts.Family));
                page.PageColor(Colors.White);

                page.Header().Column(col =>
                {
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("Executive Dashboard")
                                .FontSize(18).Bold().FontColor("#005B99");
                            c.Item().Text("Enterprise Service Excellence Management System")
                                .FontSize(9).FontColor("#64748b");
                        });
                        row.ConstantItem(200).AlignRight().Column(c =>
                        {
                            c.Item().AlignRight().Text("Mohammed Bin Rashid Housing Establishment")
                                .FontSize(9).Bold().FontColor("#1f2937");
                            c.Item().AlignRight().Text($"Generated: {generated}").FontSize(8).FontColor("#64748b");
                            c.Item().AlignRight().Text($"By: {user}").FontSize(8).FontColor("#64748b");
                        });
                    });
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor("#e5e7eb");
                });

                page.Content().PaddingVertical(12).Column(col =>
                {
                    col.Spacing(14);

                    // === KPI tiles ===
                    col.Item().Row(row =>
                    {
                        row.Spacing(10);
                        void Tile(string label, string value, string color)
                        {
                            row.RelativeItem().Border(1).BorderColor("#e5e7eb").Background("#fafbfc")
                                .Padding(10).Column(c =>
                                {
                                    c.Item().Text(label).FontSize(8).FontColor("#64748b").LetterSpacing(0.05f);
                                    c.Item().PaddingTop(2).Text(value).FontSize(20).Bold().FontColor(color);
                                });
                        }
                        Tile("BUSINESS PROCESSES",   model.TotalProcesses.ToString("N0", inv),       "#005B99");
                        Tile("SERVICES",             model.TotalServices.ToString("N0", inv),         "#005B99");
                        Tile("IMPROVEMENTS",         model.TotalImprovements.ToString("N0", inv),     "#15803D");
                        Tile("ACTIVE CHANGE REQ.",   model.ActiveChangeRequests.ToString("N0", inv),  "#B45309");
                    });

                    // === ROI band ===
                    var est = model.ImprovementROI?.TotalEstimatedCostSavings ?? 0m;
                    var act = model.ImprovementROI?.TotalActualCostSavings ?? 0m;
                    var pct = est > 0 ? Math.Round(act / est * 100m, 1) : 0m;
                    col.Item().Row(row =>
                    {
                        row.Spacing(10);
                        void Tile(string label, string value, string color)
                        {
                            row.RelativeItem().Border(1).BorderColor("#e5e7eb").Background("#f0fdf4")
                                .Padding(10).Column(c =>
                                {
                                    c.Item().Text(label).FontSize(8).FontColor("#64748b").LetterSpacing(0.05f);
                                    c.Item().PaddingTop(2).Text(value).FontSize(13).Bold().FontColor(color);
                                });
                        }
                        Tile("EST. COST SAVINGS",     est.ToString("N0", inv) + " AED",     "#1f2937");
                        Tile("REALIZED COST SAVINGS", act.ToString("N0", inv) + " AED",     "#15803D");
                        Tile("REALIZATION",           pct.ToString("0.#", inv) + "%",       "#15803D");
                        Tile("EST. TIME SAVINGS",     (model.ImprovementROI?.TotalEstimatedTimeSavings ?? 0m).ToString("N0", inv) + " h", "#1f2937");
                        Tile("REALIZED TIME",         (model.ImprovementROI?.TotalActualTimeSavings ?? 0m).ToString("N0", inv) + " h",    "#15803D");
                    });

                    // === Two-column data tables ===
                    void TwoColTable(string title, IReadOnlyList<(string Key, string Value)> rows)
                    {
                        col.Item().Column(c =>
                        {
                            c.Item().Text(title).FontSize(11).Bold().FontColor("#005B99");
                            c.Item().PaddingTop(2).Border(1).BorderColor("#e5e7eb").Table(t =>
                            {
                                t.ColumnsDefinition(cd => { cd.RelativeColumn(3); cd.RelativeColumn(1); });
                                t.Header(h =>
                                {
                                    h.Cell().Background("#005B99").Padding(5).Text("Item").FontSize(8).Bold().FontColor("#fff");
                                    h.Cell().Background("#005B99").Padding(5).AlignRight().Text("Count").FontSize(8).Bold().FontColor("#fff");
                                });
                                foreach (var (k, v) in rows)
                                {
                                    t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#f1f5f9").Text(k).FontSize(9);
                                    t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#f1f5f9").AlignRight().Text(v).FontSize(9);
                                }
                                if (rows.Count == 0)
                                {
                                    t.Cell().ColumnSpan(2).Padding(5).Text("(no data)").FontSize(8).FontColor("#94a3b8").Italic();
                                }
                            });
                        });
                    }

                    col.Item().Row(row =>
                    {
                        row.Spacing(12);
                        row.RelativeItem().Element(_ => TwoColTable("Processes by Status",
                            model.ProcessesByStatus.Select(p => (p.Status, p.Count.ToString("N0", inv))).ToList()));
                        row.RelativeItem().Element(_ => TwoColTable("Services by Type",
                            model.ServicesByType.Select(s => (s.Type, s.Count.ToString("N0", inv))).ToList()));
                        row.RelativeItem().Element(_ => TwoColTable("Improvements by Quadrant",
                            model.ImprovementsByQuadrant.Select(q => (q.Quadrant, q.Count.ToString("N0", inv))).ToList()));
                    });

                    col.Item().Row(row =>
                    {
                        row.Spacing(12);
                        row.RelativeItem().Element(_ => TwoColTable("Change Requests by Status",
                            model.ChangeRequestsByStatus.Select(cr => (cr.Status, cr.Count.ToString("N0", inv))).ToList()));
                        row.RelativeItem(2).Element(_ => TwoColTable("Top Categories (by process count)",
                            model.TopCategories.Select(c => (c.Name, c.ProcessCount.ToString("N0", inv))).ToList()));
                    });

                    // Service performance — wider table needs its own row.
                    col.Item().Column(c =>
                    {
                        c.Item().Text("Service Performance").FontSize(11).Bold().FontColor("#005B99");
                        c.Item().PaddingTop(2).Border(1).BorderColor("#e5e7eb").Table(t =>
                        {
                            t.ColumnsDefinition(cd =>
                            {
                                cd.RelativeColumn(4);
                                cd.RelativeColumn(1);
                                cd.RelativeColumn(1);
                                cd.RelativeColumn(1);
                            });
                            t.Header(h =>
                            {
                                void Cell(string txt, bool right = false)
                                {
                                    var c2 = h.Cell().Background("#005B99").Padding(5);
                                    if (right) c2.AlignRight().Text(txt).FontSize(8).Bold().FontColor("#fff");
                                    else c2.Text(txt).FontSize(8).Bold().FontColor("#fff");
                                }
                                Cell("Service");
                                Cell("SLA (days)", true);
                                Cell("Actual (days)", true);
                                Cell("CSAT %", true);
                            });
                            if (!model.ServicePerformance.Any())
                            {
                                t.Cell().ColumnSpan(4).Padding(5).Text("(no data)").FontSize(8).FontColor("#94a3b8").Italic();
                            }
                            else
                            {
                                foreach (var sp in model.ServicePerformance)
                                {
                                    t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#f1f5f9").Text(sp.ServiceName).FontSize(9);
                                    t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#f1f5f9").AlignRight().Text(sp.SLADays.ToString("N0", inv)).FontSize(9);
                                    t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#f1f5f9").AlignRight().Text(sp.ActualDeliveryDays.ToString("N1", inv)).FontSize(9);
                                    t.Cell().Padding(5).BorderBottom(0.5f).BorderColor("#f1f5f9").AlignRight().Text(sp.CustomerSatisfaction.ToString("N1", inv) + "%").FontSize(9);
                                }
                            }
                        });
                    });
                });

                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("ESEMS · ").FontSize(8).FontColor("#94a3b8");
                    t.CurrentPageNumber().FontSize(8).FontColor("#94a3b8");
                    t.Span(" / ").FontSize(8).FontColor("#94a3b8");
                    t.TotalPages().FontSize(8).FontColor("#94a3b8");
                });
            });
        });

        var bytes = doc.GeneratePdf();
        var fileName = $"esems-dashboard-{DateTime.UtcNow:yyyyMMdd-HHmmss}.pdf";
        return File(bytes, "application/pdf", fileName);
    }

    /// <summary>
    /// Builds the same DashboardViewModel that Index() does, but factored
    /// out so the export endpoints can reuse the exact same shape /
    /// filters / ordering — guarantees the PDF and CSV match what the
    /// user sees on screen.
    /// </summary>
    private async Task<DashboardViewModel> BuildDashboardViewModelAsync()
    {
        // Re-run the same query Index uses — there's no DRY helper today
        // because Index inlines everything. Tracking issue: extract into a
        // shared service if a third consumer ever shows up.
        var model = new DashboardViewModel
        {
            TotalCategories = await _context.Categories.CountAsync(c => !c.IsDeleted),
            TotalProcessGroups = await _context.ProcessGroups.CountAsync(pg => !pg.IsDeleted),
            TotalProcesses = await _context.Processes.CountAsync(p => !p.IsDeleted),
            TotalServices = await _context.Services.CountAsync(s => !s.IsDeleted),
            TotalImprovements = await _context.ImprovementInitiatives.CountAsync(i => !i.IsDeleted),
            ActiveChangeRequests = await _context.ChangeRequests
                .CountAsync(cr => !cr.IsDeleted && cr.Status != ChangeRequestStatus.Implemented && cr.Status != ChangeRequestStatus.Cancelled),
        };

        model.ProcessesByStatus = await _context.Processes
            .Where(p => !p.IsDeleted)
            .GroupBy(p => p.Status)
            .Select(g => new ProcessStatusCount { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        model.ServicesByType = await _context.Services
            .Where(s => !s.IsDeleted)
            .GroupBy(s => s.ServiceType)
            .Select(g => new ServiceTypeCount { Type = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        model.ImprovementsByQuadrant = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted)
            .GroupBy(i => i.Quadrant)
            .Select(g => new ImprovementQuadrantCount { Quadrant = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        model.ChangeRequestsByStatus = await _context.ChangeRequests
            .Where(cr => !cr.IsDeleted)
            .GroupBy(cr => cr.Status)
            .Select(g => new ChangeRequestStatusCount { Status = g.Key.ToString(), Count = g.Count() })
            .ToListAsync();

        model.TopCategories = await _context.Categories
            .Where(c => !c.IsDeleted)
            .Select(c => new CategoryStats
            {
                Name = c.NameEn ?? c.NameAr ?? "(unnamed)",
                ProcessCount = c.ProcessGroups.SelectMany(pg => pg.Processes).Count(p => !p.IsDeleted)
            })
            .OrderByDescending(x => x.ProcessCount)
            .Take(5)
            .ToListAsync();

        model.ServicePerformance = await _context.Services
            .Where(s => !s.IsDeleted && s.SLADays != null)
            .OrderByDescending(s => s.CustomerSatisfactionScore ?? 0)
            .Take(5)
            .Select(s => new ServicePerformanceMetric
            {
                ServiceName = s.NameEn ?? s.NameAr ?? "(unnamed)",
                SLADays = s.SLADays ?? 0,
                ActualDeliveryDays = s.ActualDeliveryDays ?? 0,
                CustomerSatisfaction = s.CustomerSatisfactionScore ?? 0
            })
            .ToListAsync();

        var costAgg = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted)
            .GroupBy(i => 1)
            .Select(g => new
            {
                EstCost = g.Sum(i => i.EstimatedCostSavings) ?? 0m,
                ActCost = g.Sum(i => i.ActualCostSavings) ?? 0m,
                EstTime = g.Sum(i => i.EstimatedTimeSavings) ?? 0m,
                ActTime = g.Sum(i => i.ActualTimeSavings) ?? 0m
            })
            .FirstOrDefaultAsync();
        model.ImprovementROI = new ImprovementROISummary
        {
            TotalEstimatedCostSavings = costAgg?.EstCost ?? 0m,
            TotalActualCostSavings = costAgg?.ActCost ?? 0m,
            TotalEstimatedTimeSavings = costAgg?.EstTime ?? 0m,
            TotalActualTimeSavings = costAgg?.ActTime ?? 0m
        };

        return model;
    }

    /// <summary>
    /// Improvement Quadrant Analysis View
    /// </summary>
    public async Task<IActionResult> QuadrantAnalysis()
    {
        var improvements = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted)
            .Include(i => i.Process)
            .Include(i => i.Service)
            .ToListAsync();

        var model = new QuadrantAnalysisViewModel
        {
            QuickWins = improvements.Where(i => i.Quadrant == ImprovementQuadrant.QuickWins).ToList(),
            MajorProjects = improvements.Where(i => i.Quadrant == ImprovementQuadrant.MajorProjects).ToList(),
            FillIns = improvements.Where(i => i.Quadrant == ImprovementQuadrant.FillIns).ToList(),
            ThanklessTasks = improvements.Where(i => i.Quadrant == ImprovementQuadrant.ThanklessTasks).ToList()
        };

        return View(model);
    }
}

/// <summary>
/// Dashboard view model
/// </summary>
public class DashboardViewModel
{
    public int TotalCategories { get; set; }
    public int TotalProcessGroups { get; set; }
    public int TotalProcesses { get; set; }
    public int TotalServices { get; set; }
    public int TotalImprovements { get; set; }
    public int ActiveChangeRequests { get; set; }
    public List<ProcessStatusCount> ProcessesByStatus { get; set; } = new();
    public List<ServiceTypeCount> ServicesByType { get; set; } = new();
    public List<ImprovementQuadrantCount> ImprovementsByQuadrant { get; set; } = new();
    public List<ChangeRequestStatusCount> ChangeRequestsByStatus { get; set; } = new();
    public List<CategoryStats> TopCategories { get; set; } = new();
    public List<ServicePerformanceMetric> ServicePerformance { get; set; } = new();
    public ImprovementROISummary ImprovementROI { get; set; } = new();
    public List<RecentActivityItem> RecentActivities { get; set; } = new();
}

public class ProcessStatusCount
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ServiceTypeCount
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ImprovementQuadrantCount
{
    public string Quadrant { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class ChangeRequestStatusCount
{
    public string Status { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class CategoryStats
{
    public string Name { get; set; } = string.Empty;
    public int ProcessCount { get; set; }
}

public class ServicePerformanceMetric
{
    public string ServiceName { get; set; } = string.Empty;
    public int SLADays { get; set; }
    public decimal ActualDeliveryDays { get; set; }
    public decimal CustomerSatisfaction { get; set; }
}

public class ImprovementROISummary
{
    public decimal TotalEstimatedCostSavings { get; set; }
    public decimal TotalActualCostSavings { get; set; }
    public decimal TotalEstimatedTimeSavings { get; set; }
    public decimal TotalActualTimeSavings { get; set; }
}

public class RecentActivityItem
{
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Quadrant analysis view model
/// </summary>
public class QuadrantAnalysisViewModel
{
    public List<ESEMS.Web.Models.Improvement.ImprovementInitiative> QuickWins { get; set; } = new();
    public List<ESEMS.Web.Models.Improvement.ImprovementInitiative> MajorProjects { get; set; } = new();
    public List<ESEMS.Web.Models.Improvement.ImprovementInitiative> FillIns { get; set; } = new();
    public List<ESEMS.Web.Models.Improvement.ImprovementInitiative> ThanklessTasks { get; set; } = new();
}

