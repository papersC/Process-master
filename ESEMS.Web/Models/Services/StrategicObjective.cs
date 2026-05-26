using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.ServiceManagement;

namespace ESEMS.Web.Models.Services;

/// <summary>
/// Strategic objective entity for linking services and processes to organizational goals
/// </summary>
public class StrategicObjective : AuditableBilingualEntity
{
    /// <summary>
    /// Objective code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Parent objective ID (for hierarchy)
    /// </summary>
    public string? ParentId { get; set; }

    /// <summary>
    /// Level in the hierarchy (1=Strategic, 2=Tactical, 3=Operational)
    /// </summary>
    [Range(1, 3)]
    public int Level { get; set; } = 1;

    /// <summary>
    /// Target year. Wide sane window — anything outside is almost
    /// certainly a typo (a year like 2 or 99999 would break sorting on
    /// any roadmap that orders by year).
    /// </summary>
    [Range(2000, 2100)]
    public int? TargetYear { get; set; }

    /// <summary>
    /// Target value (if measurable)
    /// </summary>
    public decimal? TargetValue { get; set; }

    /// <summary>
    /// Current value (if measurable)
    /// </summary>
    public decimal? CurrentValue { get; set; }

    /// <summary>
    /// Unit of measurement
    /// </summary>
    public string? UnitOfMeasure { get; set; }

    /// <summary>
    /// Whether this objective is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Owning organizational unit ID
    /// </summary>
    public int? OwningUnitId { get; set; }

    /// <summary>
    /// Tags for categorization (e.g., "Partner", "Project")
    /// </summary>
    public string? Tags { get; set; }

    // Navigation properties
    public StrategicObjective? Parent { get; set; }
    public OrganizationUnit? OwningUnit { get; set; }
    public ICollection<StrategicObjective> Children { get; set; } = new List<StrategicObjective>();
    public ICollection<Service> Services { get; set; } = new List<Service>();
    public ICollection<Process> Processes { get; set; } = new List<Process>();
    public ICollection<ProcessStrategicObjective> ProcessStrategicObjectives { get; set; } = new List<ProcessStrategicObjective>();
    public ICollection<ServiceStrategicObjective> ServiceStrategicObjectives { get; set; } = new List<ServiceStrategicObjective>();

    /// <summary>
    /// Gets the progress percentage, clamped to [0, 100].
    /// Over-achievement (>100%) and below-zero ratios both read as data errors
    /// in a strategic dashboard, so they are pinned to the bounds rather than
    /// rendered literally (e.g. 800%).
    /// </summary>
    public decimal? GetProgressPercentage()
    {
        if (!TargetValue.HasValue || TargetValue.Value == 0 || !CurrentValue.HasValue)
            return null;
        var pct = (CurrentValue.Value / TargetValue.Value) * 100m;
        return Math.Clamp(pct, 0m, 100m);
    }

    /// <summary>
    /// Gets the level name based on the level number
    /// </summary>
    public string GetLevelName()
    {
        return Level switch
        {
            1 => "Strategic",
            2 => "Tactical",
            3 => "Operational",
            _ => "Objective"
        };
    }
}

