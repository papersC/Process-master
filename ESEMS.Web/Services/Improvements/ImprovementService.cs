using System.Globalization;
using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Data;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Models.ViewModels;

namespace ESEMS.Web.Services.Improvements;

public class ImprovementWorkflowService : IImprovementWorkflowService
{
    private readonly ApplicationDbContext _context;

    public ImprovementWorkflowService(ApplicationDbContext context)
    {
        _context = context;
    }

    public WizardValidationResult ValidateWizardInput(
        ImprovementInitiative improvement,
        string[]? selectedProcessIds,
        string[]? selectedServiceIds,
        WizardMeasurementParams mp)
    {
        var result = new WizardValidationResult { IsValid = true };

        // Normalize selected processes/services
        var processIds = (selectedProcessIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var serviceIds = (selectedServiceIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (!string.IsNullOrWhiteSpace(improvement.ProcessId))
            processIds.Add(improvement.ProcessId.Trim());
        if (!string.IsNullOrWhiteSpace(improvement.ServiceId))
            serviceIds.Add(improvement.ServiceId.Trim());

        result.NormalizedProcessIds = processIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.NormalizedServiceIds = serviceIds.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        if (result.NormalizedProcessIds.Count == 0 && result.NormalizedServiceIds.Count == 0)
            result.Errors.Add("Please select at least one Process or Service.");

        if (string.IsNullOrWhiteSpace(improvement.TitleEn))
            result.Errors.Add("Title (English) is required.");
        if (string.IsNullOrWhiteSpace(improvement.TitleAr))
            result.Errors.Add("Title (Arabic) is required.");

        // Parse and validate measurements
        var types = (mp.MeasurementTypes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var names = (mp.MeasurementNames ?? "").Split('|', StringSplitOptions.None);
        var namesAr = (mp.MeasurementNamesAr ?? "").Split('|', StringSplitOptions.None);
        var units = (mp.MeasurementUnits ?? "").Split('|', StringSplitOptions.None);
        var targets = (mp.MeasurementTargets ?? "").Split(',', StringSplitOptions.None);
        var asIs = (mp.MeasurementAsIs ?? "").Split(',', StringSplitOptions.None);

        if (types.Length == 0)
            result.Errors.Add("Please add at least one measurement.");

        for (int idx = 0; idx < types.Length; idx++)
        {
            var nameEn = (idx < names.Length ? names[idx] : "").Trim();
            var nameAr = (idx < namesAr.Length ? namesAr[idx] : "").Trim();
            var unit = (idx < units.Length ? units[idx] : "").Trim();

            if (!Enum.TryParse<ImprovementMeasurementType>(types[idx], out _))
                result.Errors.Add($"Invalid measurement type at row {idx + 1}.");

            if (string.IsNullOrWhiteSpace(unit))
                result.Errors.Add($"Unit of measure is required for measurement {idx + 1}.");

            if (string.IsNullOrWhiteSpace(nameEn) && string.IsNullOrWhiteSpace(nameAr))
                result.Errors.Add($"Name (English or Arabic) is required for measurement {idx + 1}.");

            if (idx >= targets.Length || string.IsNullOrWhiteSpace(targets[idx]) || !decimal.TryParse(targets[idx], out _))
                result.Errors.Add($"Target value is required for measurement {idx + 1}.");

            if (idx >= asIs.Length || string.IsNullOrWhiteSpace(asIs[idx]) || !decimal.TryParse(asIs[idx], out _))
                result.Errors.Add($"As-Is value is required for measurement {idx + 1}.");
        }

        result.IsValid = result.Errors.Count == 0;
        return result;
    }

    public async Task SaveWizardImprovementAsync(
        ImprovementInitiative improvement,
        List<string> processIds,
        List<string> serviceIds,
        WizardMeasurementParams mp,
        string? userId)
    {
        improvement.Id = Guid.NewGuid().ToString();
        improvement.CreatedAt = DateTime.UtcNow;
        improvement.UpdatedAt = DateTime.UtcNow;
        improvement.CreatedById = userId;
        improvement.ProcessId = processIds.FirstOrDefault();
        improvement.ServiceId = serviceIds.FirstOrDefault();
        // Audit #17: cut-offs are applied by the controller before this call
        // so the active PrioritizationConfig wins. Recompute here as a safety
        // net only if Quadrant is still at its default (FillIns) — preserves
        // the pre-#17 behaviour for callers that don't pre-stamp Quadrant.
        if (improvement.Quadrant == ImprovementQuadrant.FillIns)
            improvement.CalculateQuadrant();

        _context.ImprovementInitiatives.Add(improvement);

        // Many-to-many links
        foreach (var processId in processIds)
        {
            _context.ImprovementProcesses.Add(new ImprovementProcess
            {
                ImprovementId = improvement.Id,
                ProcessId = processId,
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId
            });
        }
        foreach (var serviceId in serviceIds)
        {
            _context.ImprovementServices.Add(new Models.Improvement.ImprovementService
            {
                ImprovementId = improvement.Id,
                ServiceId = serviceId,
                CreatedAt = DateTime.UtcNow,
                CreatedById = userId
            });
        }

        // Parse and save measurements
        var types = (mp.MeasurementTypes ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        var names = (mp.MeasurementNames ?? "").Split('|', StringSplitOptions.None);
        var namesAr = (mp.MeasurementNamesAr ?? "").Split('|', StringSplitOptions.None);
        var units = (mp.MeasurementUnits ?? "").Split('|', StringSplitOptions.None);
        var targets = (mp.MeasurementTargets ?? "").Split(',', StringSplitOptions.None);
        var asIs = (mp.MeasurementAsIs ?? "").Split(',', StringSplitOptions.None);
        var toBe = (mp.MeasurementToBe ?? "").Split(',', StringSplitOptions.None);
        var weights = (mp.MeasurementWeights ?? "").Split(',', StringSplitOptions.None);
        var appliesTo = (mp.MeasurementAppliesTo ?? "").Split('|', StringSplitOptions.None);
        var periods = (mp.MeasurementPeriods ?? "").Split('|', StringSplitOptions.None);
        var methods = (mp.MeasurementMethods ?? "").Split('|', StringSplitOptions.None);
        var bpmn = (mp.MeasurementBpmn ?? "").Split('|', StringSplitOptions.None);
        var priorities = (mp.MeasurementPriorities ?? "").Split('|', StringSplitOptions.None);
        var directions = (mp.MeasurementDirections ?? "").Split('|', StringSplitOptions.None);
        // KpiDefinitionIds: pipe-joined; empty entries mean "not catalog-linked".
        var kpiIds = (mp.MeasurementKpiDefinitionIds ?? "").Split('|', StringSplitOptions.None);

        for (int idx = 0; idx < types.Length; idx++)
        {
            if (Enum.TryParse<ImprovementMeasurementType>(types[idx], out var mType))
            {
                // Normalize direction; anything unexpected falls back to HigherBetter.
                var rawDir = idx < directions.Length ? (directions[idx] ?? "").Trim() : "";
                var normalizedDir = string.Equals(rawDir, "LowerBetter", StringComparison.OrdinalIgnoreCase)
                    ? MeasurementDirection.LowerBetter
                    : MeasurementDirection.HigherBetter;

                // Audit #13: AppliesTo magic-string ("process:{id}" / "service:{id}")
                // is replaced by real FKs. Parse the wizard payload accordingly.
                var rawApplies = idx < appliesTo.Length ? (appliesTo[idx] ?? "").Trim() : "";
                string? applProcId = null, applSvcId = null;
                if (rawApplies.StartsWith("process:", StringComparison.OrdinalIgnoreCase))
                    applProcId = rawApplies.Substring("process:".Length);
                else if (rawApplies.StartsWith("service:", StringComparison.OrdinalIgnoreCase))
                    applSvcId = rawApplies.Substring("service:".Length);

                // Catalog link — empty → null (free-text measurement).
                var rawKpiId = idx < kpiIds.Length ? (kpiIds[idx] ?? "").Trim() : "";
                var kpiDefId = string.IsNullOrEmpty(rawKpiId) ? null : rawKpiId;

                var m = new ImprovementMeasurement
                {
                    Id = Guid.NewGuid().ToString(),
                    ImprovementId = improvement.Id,
                    MeasurementType = mType,
                    NameEn = idx < names.Length ? names[idx] : "",
                    NameAr = idx < namesAr.Length ? namesAr[idx] : "",
                    UnitOfMeasure = idx < units.Length ? units[idx] : "",
                    TargetValue = idx < targets.Length && decimal.TryParse(targets[idx], out var tv) ? tv : null,
                    AsIsValue = idx < asIs.Length && decimal.TryParse(asIs[idx], out var av) ? av : null,
                    ToBeValue = idx < toBe.Length && decimal.TryParse(toBe[idx], out var bv) ? bv : null,
                    Weight = idx < weights.Length && int.TryParse(weights[idx], out var w) ? w : 0,
                    AppliesToProcessId = applProcId,
                    AppliesToServiceId = applSvcId,
                    MeasuringPeriod = (idx < periods.Length && !string.IsNullOrWhiteSpace(periods[idx])) ? periods[idx] : "Monthly",
                    MeasuringMethod = idx < methods.Length ? methods[idx] : null,
                    BpmnReference = idx < bpmn.Length ? bpmn[idx] : null,
                    Priority = idx < priorities.Length ? priorities[idx] : null,
                    Direction = normalizedDir,
                    KpiDefinitionId = kpiDefId,
                    DisplayOrder = idx,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.ImprovementMeasurements.Add(m);
            }
        }

        await _context.SaveChangesAsync();
    }

    public RoadmapViewModel GenerateRoadmap(List<ImprovementInitiative> all)
    {
        var vm = new RoadmapViewModel
        {
            AllInitiatives = all,
            TotalInitiatives = all.Count,
            CompletedInitiatives = all.Count(i => i.Status == ImprovementStatus.Completed),
            OverallProgress = all.Any() ? all.Average(i => i.ProgressPercentage) : 0,
            TotalEstimatedCostSavings = all.Sum(i => i.EstimatedCostSavings ?? 0),
            TotalActualCostSavings = all.Sum(i => i.ActualCostSavings ?? 0),
            TotalEstimatedTimeSavings = all.Sum(i => i.EstimatedTimeSavings ?? 0),
            TotalActualTimeSavings = all.Sum(i => i.ActualTimeSavings ?? 0),
            QuickWinsCount = all.Count(i => i.Quadrant == ImprovementQuadrant.QuickWins),
            MajorProjectsCount = all.Count(i => i.Quadrant == ImprovementQuadrant.MajorProjects),
            FillInsCount = all.Count(i => i.Quadrant == ImprovementQuadrant.FillIns),
            ThanklessTasksCount = all.Count(i => i.Quadrant == ImprovementQuadrant.ThanklessTasks),
            Horizon1Count = all.Count(i => i.Horizon == ImprovementHorizon.Horizon1_Current),
            Horizon2Count = all.Count(i => i.Horizon == ImprovementHorizon.Horizon2_Expand),
            Horizon3Count = all.Count(i => i.Horizon == ImprovementHorizon.Horizon3_Future),
            UnclassifiedCount = all.Count(i => !i.Horizon.HasValue)
        };

        // Phase assignment: 3 Horizons (McKinsey / DGEP best practice)
        // Within each horizon, sort by Quadrant priority then by TotalPrioritizationScore
        var quadrantOrder = new Dictionary<ImprovementQuadrant, int>
        {
            { ImprovementQuadrant.QuickWins, 1 },
            { ImprovementQuadrant.MajorProjects, 2 },
            { ImprovementQuadrant.FillIns, 3 },
            { ImprovementQuadrant.ThanklessTasks, 4 }
        };

        var assigned = new Dictionary<int, List<ImprovementInitiative>>
        {
            { 1, new() }, { 2, new() }, { 3, new() }
        };

        foreach (var item in all)
        {
            int phase = item.Horizon switch
            {
                ImprovementHorizon.Horizon1_Current => 1,
                ImprovementHorizon.Horizon2_Expand => 2,
                ImprovementHorizon.Horizon3_Future => 3,
                _ => 1 // Unclassified defaults to current horizon
            };
            assigned[phase].Add(item);
        }

        foreach (var kvp in assigned)
        {
            assigned[kvp.Key] = kvp.Value
                .OrderBy(i => quadrantOrder.GetValueOrDefault(i.Quadrant, 3))
                .ThenByDescending(i => i.TotalPrioritizationScore ?? 0)
                .ThenByDescending(i => i.ImpactScore)
                .ThenBy(i => i.EffortScore)
                .ToList();
        }

        vm.Phases = new List<RoadmapPhase>
        {
            new() { PhaseNumber = 1, Name = "Horizon 1: Enhance Current", NameAr = "الأفق 1: تعزيز الأعمال الحالية",
                Description = "Enhance current business model \u2014 quick wins & operational improvements (0\u201312 months)",
                DescriptionAr = "تعزيز نموذج الأعمال الحالي \u2014 مكاسب سريعة وتحسينات تشغيلية (0-12 شهراً)",
                Color = "#22c55e", BgColor = "#f0fdf4", Icon = "zap",
                TimelineLabel = "0 \u2013 12 months", TimelineLabelAr = "0 \u2013 12 شهراً",
                Initiatives = assigned[1] },
            new() { PhaseNumber = 2, Name = "Horizon 2: Expand & Experiment", NameAr = "الأفق 2: التوسع والتجريب",
                Description = "Expand business model \u2014 strategic projects & new capabilities (1\u20133 years)",
                DescriptionAr = "توسيع نموذج الأعمال \u2014 مشاريع استراتيجية وقدرات جديدة (1-3 سنوات)",
                Color = "#3b82f6", BgColor = "#eff6ff", Icon = "rocket",
                TimelineLabel = "1 \u2013 3 years", TimelineLabelAr = "1 \u2013 3 سنوات",
                Initiatives = assigned[2] },
            new() { PhaseNumber = 3, Name = "Horizon 3: Future Vision", NameAr = "الأفق 3: رؤية المستقبل",
                Description = "Future business model \u2014 transformational & innovation initiatives (3+ years)",
                DescriptionAr = "نموذج الأعمال المستقبلي \u2014 مبادرات تحولية وابتكارية (3+ سنوات)",
                Color = "#8b5cf6", BgColor = "#f5f3ff", Icon = "telescope",
                TimelineLabel = "3+ years", TimelineLabelAr = "3+ سنوات",
                Initiatives = assigned[3] }
        };

        // Cumulative savings projection
        decimal running = 0;
        foreach (var phase in vm.Phases)
        {
            running += phase.TotalEstimatedCostSavings;
            vm.CumulativeSavings.Add(running);
        }

        // Scatter chart data
        bool isAr = CultureInfo.CurrentUICulture.Name.StartsWith("ar");
        foreach (var item in all)
        {
            vm.ScatterData.Add(new ScatterPoint
            {
                Id = item.Id,
                Label = isAr ? item.TitleAr : item.TitleEn,
                ImpactScore = item.ImpactScore,
                EffortScore = item.EffortScore,
                PrioritizationScore = item.TotalPrioritizationScore ?? 0,
                Quadrant = item.Quadrant.ToString(),
                Horizon = item.Horizon?.ToString() ?? "Unclassified",
                ProgressPercentage = item.ProgressPercentage,
                Status = item.Status.ToString()
            });
        }

        // Gantt data
        foreach (var phase in vm.Phases)
        {
            foreach (var item in phase.Initiatives)
            {
                vm.GanttData.Add(new GanttItem
                {
                    Id = item.Id,
                    Label = isAr ? item.TitleAr : item.TitleEn,
                    PhaseNumber = phase.PhaseNumber,
                    Quadrant = item.Quadrant.ToString(),
                    Horizon = item.Horizon?.ToString() ?? "Unclassified",
                    StartDate = item.CreatedAt.ToString("yyyy-MM-dd"),
                    EndDate = (item.TargetDate ?? item.CreatedAt.AddMonths(6 * phase.PhaseNumber)).ToString("yyyy-MM-dd"),
                    ProgressPercentage = item.ProgressPercentage,
                    Color = phase.Color
                });
            }
        }

        return vm;
    }

    public async Task<DashboardMetrics> CalculateDashboardMetricsAsync()
    {
        var improvements = await _context.ImprovementInitiatives
            .Where(i => !i.IsDeleted)
            .Include(i => i.Process)
            .Include(i => i.Service)
            .Include(i => i.OwningUnit)
            .ToListAsync();

        return new DashboardMetrics
        {
            TotalInitiatives = improvements.Count,
            QuickWinsCount = improvements.Count(i => i.Quadrant == ImprovementQuadrant.QuickWins),
            MajorProjectsCount = improvements.Count(i => i.Quadrant == ImprovementQuadrant.MajorProjects),
            FillInsCount = improvements.Count(i => i.Quadrant == ImprovementQuadrant.FillIns),
            ThanklessTasksCount = improvements.Count(i => i.Quadrant == ImprovementQuadrant.ThanklessTasks),
            H1Count = improvements.Count(i => i.Horizon == ImprovementHorizon.Horizon1_Current),
            H2Count = improvements.Count(i => i.Horizon == ImprovementHorizon.Horizon2_Expand),
            H3Count = improvements.Count(i => i.Horizon == ImprovementHorizon.Horizon3_Future),
            TopPriorities = improvements
                .OrderByDescending(i => i.TotalPrioritizationScore)
                .Take(5)
                .ToList()
        };
    }
}
