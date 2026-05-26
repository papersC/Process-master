using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Improvement;

namespace ESEMS.Web.Models.ViewModels;

/// <summary>
/// Auto-generated roadmap phase with intelligent prioritization
/// </summary>
public class RoadmapPhase
{
    public int PhaseNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DescriptionAr { get; set; } = string.Empty;
    public string Color { get; set; } = "#6b7280";
    public string BgColor { get; set; } = "#f3f4f6";
    public string Icon { get; set; } = "flag";
    public string TimelineLabel { get; set; } = string.Empty;
    public string TimelineLabelAr { get; set; } = string.Empty;
    public List<ImprovementInitiative> Initiatives { get; set; } = new();

    // Computed KPIs
    public decimal TotalEstimatedCostSavings => Initiatives.Sum(i => i.EstimatedCostSavings ?? 0);
    public decimal TotalEstimatedTimeSavings => Initiatives.Sum(i => i.EstimatedTimeSavings ?? 0);
    public double AvgProgress => Initiatives.Any() ? Initiatives.Average(i => i.ProgressPercentage) : 0;
    public int CompletedCount => Initiatives.Count(i => i.Status == ImprovementStatus.Completed);
    public int InProgressCount => Initiatives.Count(i => i.Status == ImprovementStatus.InProgress);
    public decimal AvgPrioritizationScore => Initiatives.Any()
        ? Initiatives.Where(i => i.TotalPrioritizationScore.HasValue)
            .Select(i => i.TotalPrioritizationScore!.Value)
            .DefaultIfEmpty(0).Average()
        : 0;
}

/// <summary>
/// Complete roadmap view model with auto-generated phases, KPI projections, and chart data
/// </summary>
public class RoadmapViewModel
{
    public List<ImprovementInitiative> AllInitiatives { get; set; } = new();
    public List<RoadmapPhase> Phases { get; set; } = new();

    // Quadrant summary
    public int QuickWinsCount { get; set; }
    public int MajorProjectsCount { get; set; }
    public int FillInsCount { get; set; }
    public int ThanklessTasksCount { get; set; }

    // Horizon summary
    public int Horizon1Count { get; set; }
    public int Horizon2Count { get; set; }
    public int Horizon3Count { get; set; }
    public int UnclassifiedCount { get; set; }

    // Aggregate KPIs
    public decimal TotalEstimatedCostSavings { get; set; }
    public decimal TotalActualCostSavings { get; set; }
    public decimal TotalEstimatedTimeSavings { get; set; }
    public decimal TotalActualTimeSavings { get; set; }
    public double OverallProgress { get; set; }
    public int TotalInitiatives { get; set; }
    public int CompletedInitiatives { get; set; }

    // Scatter chart data (Impact vs Effort)
    public List<ScatterPoint> ScatterData { get; set; } = new();

    // Gantt chart data
    public List<GanttItem> GanttData { get; set; } = new();

    // Cumulative savings projection per phase
    public List<decimal> CumulativeSavings { get; set; } = new();
}

/// <summary>
/// Data point for Impact vs Effort scatter chart
/// </summary>
public class ScatterPoint
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int ImpactScore { get; set; }
    public int EffortScore { get; set; }
    public decimal PrioritizationScore { get; set; }
    public string Quadrant { get; set; } = string.Empty;
    public string Horizon { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// Data item for Gantt timeline visualization
/// </summary>
public class GanttItem
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int PhaseNumber { get; set; }
    public string Quadrant { get; set; } = string.Empty;
    public string Horizon { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
    public int ProgressPercentage { get; set; }
    public string Color { get; set; } = string.Empty;
}

