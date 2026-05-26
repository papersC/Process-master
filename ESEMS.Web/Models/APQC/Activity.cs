using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.APQC;

/// <summary>
/// APQC Level 4 - Activity (Optional)
/// Major step within a process
/// </summary>
public class Activity : MeasurableEntity
{
    /// <summary>
    /// Activity code (e.g., "1.1.1.1", "1.1.1.2")
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Parent process ID
    /// </summary>
    public string ProcessId { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Channel type (Digital, Physical, Hybrid)
    /// </summary>
    public ChannelType ChannelType { get; set; } = ChannelType.Hybrid;

    /// <summary>
    /// Owning organizational unit ID
    /// </summary>
    public int? OwningUnitId { get; set; }

    /// <summary>
    /// Multiple configurable tags (comma-separated)
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Aggregated total duration in minutes from child tasks
    /// </summary>
    public decimal? AggregatedDurationMinutes { get; set; }

    /// <summary>
    /// Aggregated total cost from child tasks
    /// </summary>
    public decimal? AggregatedCost { get; set; }

    /// <summary>
    /// Whether this activity has optional Level 5 breakdown
    /// </summary>
    public bool HasDetailedBreakdown { get; set; }

    // Navigation properties
    public Process? Process { get; set; }
    public OrganizationUnit? OwningUnit { get; set; }
    public ICollection<ProcessTask> Tasks { get; set; } = new List<ProcessTask>();
    public ICollection<ActivityRaci> RaciMatrix { get; set; } = new List<ActivityRaci>();

    /// <summary>
    /// Gets the list of tags
    /// </summary>
    public List<string> GetTagList()
    {
        if (string.IsNullOrEmpty(Tags))
            return new List<string>();
        return Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                   .Select(t => t.Trim())
                   .ToList();
    }

    /// <summary>
    /// Sets tags from a list
    /// </summary>
    public void SetTagList(IEnumerable<string> tags)
    {
        Tags = string.Join(",", tags.Where(t => !string.IsNullOrWhiteSpace(t)));
    }
}

