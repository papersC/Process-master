using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.APQC;

/// <summary>
/// APQC Level 2 - Process Group
/// Major business capability within a category
/// </summary>
public class ProcessGroup : AuditableBilingualEntity
{
    /// <summary>
    /// Process group code — "{Cat.Code}.{Y}" in the hierarchical scheme
    /// (e.g. "1.1", "1.2"). Auto-generated; not user-editable.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Zero-padded Code for natural sort (e.g. "0001.0002").
    /// </summary>
    public string? SortKey { get; set; }

    /// <summary>
    /// Parent category ID
    /// </summary>
    public string CategoryId { get; set; } = string.Empty;

    /// <summary>
    /// Multiple configurable tags (comma-separated)
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Aggregated total duration in minutes from child processes
    /// </summary>
    public decimal? AggregatedDurationMinutes { get; set; }

    /// <summary>
    /// Aggregated total cost from child processes
    /// </summary>
    public decimal? AggregatedCost { get; set; }

    /// <summary>
    /// Whether this process group contains automated processes
    /// </summary>
    public bool HasAutomatedProcesses { get; set; }

    // Navigation properties
    public Category? Category { get; set; }
    public ICollection<Process> Processes { get; set; } = new List<Process>();

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

