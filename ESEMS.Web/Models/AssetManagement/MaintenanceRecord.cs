using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.AssetManagement;

/// <summary>
/// Represents a maintenance record for an asset (ISO 55001:2014)
/// </summary>
public class MaintenanceRecord : BilingualEntity, IValidatableObject
{
    /// <summary>
    /// Asset ID
    /// </summary>
    public string AssetId { get; set; } = string.Empty;

    /// <summary>
    /// Maintenance schedule ID (if scheduled maintenance)
    /// </summary>
    public string? MaintenanceScheduleId { get; set; }

    /// <summary>
    /// Maintenance type
    /// </summary>
    public MaintenanceType Type { get; set; } = MaintenanceType.Preventive;

    /// <summary>
    /// Performed date
    /// </summary>
    public DateTime PerformedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Performed by user ID
    /// </summary>
    public string? PerformedById { get; set; }

    /// <summary>
    /// Vendor/contractor name (if external)
    /// </summary>
    public string? VendorName { get; set; }

    /// <summary>
    /// Duration (in hours)
    /// </summary>
    public decimal? DurationHours { get; set; }

    /// <summary>
    /// Cost
    /// </summary>
    public decimal? Cost { get; set; }

    /// <summary>
    /// Work performed description
    /// </summary>
    public string? WorkPerformed { get; set; }

    /// <summary>
    /// Parts replaced
    /// </summary>
    public string? PartsReplaced { get; set; }

    /// <summary>
    /// Issues found
    /// </summary>
    public string? IssuesFound { get; set; }

    /// <summary>
    /// Recommendations
    /// </summary>
    public string? Recommendations { get; set; }

    /// <summary>
    /// Next maintenance due date
    /// </summary>
    public DateTime? NextMaintenanceDue { get; set; }

    /// <summary>
    /// Downtime (in hours)
    /// </summary>
    public decimal? DowntimeHours { get; set; }

    /// <summary>
    /// Completion status
    /// </summary>
    public bool IsCompleted { get; set; } = true;

    // Navigation properties
    public Asset? Asset { get; set; }
    public MaintenanceSchedule? MaintenanceSchedule { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? PerformedBy { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        // Maintenance can't have been performed in the future. 1-day grace
        // window accommodates time-zone offsets between client and server.
        if (PerformedDate.Date > DateTime.UtcNow.Date.AddDays(1))
        {
            yield return new ValidationResult(
                "Performed date cannot be in the future.",
                new[] { nameof(PerformedDate) });
        }
    }
}

