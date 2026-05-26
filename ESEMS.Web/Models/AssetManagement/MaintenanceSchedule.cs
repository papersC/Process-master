using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.AssetManagement;

/// <summary>
/// Represents a maintenance schedule for an asset (ISO 55001:2014)
/// </summary>
public class MaintenanceSchedule : AuditableBilingualEntity
{
    /// <summary>
    /// Asset ID
    /// </summary>
    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// Maintenance type
    /// </summary>
    public MaintenanceType Type { get; set; } = MaintenanceType.Preventive;

    /// <summary>
    /// Frequency in days
    /// </summary>
    public int FrequencyDays { get; set; } = 30;

    /// <summary>
    /// Last performed date
    /// </summary>
    public DateTime? LastPerformedDate { get; set; }

    /// <summary>
    /// Next scheduled date
    /// </summary>
    public DateTime NextScheduledDate { get; set; }

    /// <summary>
    /// Estimated duration (in hours)
    /// </summary>
    public decimal? EstimatedDurationHours { get; set; }

    /// <summary>
    /// Estimated cost
    /// </summary>
    public decimal? EstimatedCost { get; set; }

    /// <summary>
    /// Assigned to user ID
    /// </summary>
    public string? AssignedToId { get; set; }

    /// <summary>
    /// Is active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Instructions
    /// </summary>
    public string? Instructions { get; set; }

    // Navigation properties
    public Asset? Asset { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? AssignedTo { get; set; }

    /// <summary>
    /// Calculate next scheduled date based on frequency
    /// </summary>
    public void CalculateNextScheduledDate()
    {
        var baseDate = LastPerformedDate ?? CreatedAt;
        NextScheduledDate = baseDate.AddDays(FrequencyDays);
    }

    /// <summary>
    /// Check if maintenance is overdue
    /// </summary>
    public bool IsOverdue()
    {
        return IsActive && NextScheduledDate < DateTime.UtcNow;
    }
}

