using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Models.ViewModels;

namespace ESEMS.Web.Services.Improvements;

public class WizardMeasurementParams
{
    public string? MeasurementTypes { get; set; }
    public string? MeasurementNames { get; set; }
    public string? MeasurementNamesAr { get; set; }
    public string? MeasurementUnits { get; set; }
    public string? MeasurementTargets { get; set; }
    public string? MeasurementAsIs { get; set; }
    public string? MeasurementToBe { get; set; }
    public string? MeasurementWeights { get; set; }
    public string? MeasurementAppliesTo { get; set; }
    public string? MeasurementPeriods { get; set; }
    public string? MeasurementMethods { get; set; }
    public string? MeasurementBpmn { get; set; }
    public string? MeasurementPriorities { get; set; }
    /// <summary>
    /// Pipe-separated direction-of-improvement per measurement:
    /// "HigherBetter" or "LowerBetter".
    /// </summary>
    public string? MeasurementDirections { get; set; }

    /// <summary>
    /// Pipe-separated KpiDefinition.Id per measurement (empty string means
    /// the row is free-text / not catalog-linked). Carries the link from
    /// the wizard's "Link to KPI" picker so the saved row's
    /// ImprovementMeasurement.KpiDefinitionId is populated and cross-improvement
    /// aggregation can group on the same canonical KPI.
    /// </summary>
    public string? MeasurementKpiDefinitionIds { get; set; }
}

public class WizardValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> NormalizedProcessIds { get; set; } = new();
    public List<string> NormalizedServiceIds { get; set; } = new();
}

public class DashboardMetrics
{
    public int TotalInitiatives { get; set; }
    public int QuickWinsCount { get; set; }
    public int MajorProjectsCount { get; set; }
    public int FillInsCount { get; set; }
    public int ThanklessTasksCount { get; set; }
    public int H1Count { get; set; }
    public int H2Count { get; set; }
    public int H3Count { get; set; }
    public List<ImprovementInitiative> TopPriorities { get; set; } = new();
}

public interface IImprovementWorkflowService
{
    WizardValidationResult ValidateWizardInput(
        ImprovementInitiative improvement,
        string[]? selectedProcessIds,
        string[]? selectedServiceIds,
        WizardMeasurementParams measurementParams);

    Task SaveWizardImprovementAsync(
        ImprovementInitiative improvement,
        List<string> processIds,
        List<string> serviceIds,
        WizardMeasurementParams measurementParams,
        string? userId);

    RoadmapViewModel GenerateRoadmap(List<ImprovementInitiative> all);

    Task<DashboardMetrics> CalculateDashboardMetricsAsync();
}
