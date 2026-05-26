using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.AssetManagement;

namespace ESEMS.Web.Models.ServiceManagement;

/// <summary>
/// Represents an incident in the IT Service Management system (ISO 20000-1:2018)
/// </summary>
public class Incident : AuditableBilingualEntity, Common.IAssignedToUnit
{
    /// <summary>
    /// Incident number (auto-generated, e.g., INC-2026-0001)
    /// </summary>
    public string IncidentNumber { get; set; } = string.Empty;

    /// <summary>
    /// Priority level (1=Critical, 2=High, 3=Medium, 4=Low)
    /// </summary>
    [Range(1, 4)]
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Impact level (1=Critical, 2=High, 3=Medium, 4=Low)
    /// </summary>
    [Range(1, 4)]
    public int Impact { get; set; } = 3;

    /// <summary>
    /// Urgency level (1=Critical, 2=High, 3=Medium, 4=Low)
    /// </summary>
    [Range(1, 4)]
    public int Urgency { get; set; } = 3;

    /// <summary>
    /// Current status
    /// </summary>
    public IncidentStatus Status { get; set; } = IncidentStatus.New;

    /// <summary>
    /// Category of the incident
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// Subcategory for more specific classification
    /// </summary>
    public string? Subcategory { get; set; }

    /// <summary>
    /// Affected service ID
    /// </summary>
    public string? ServiceId { get; set; }

    /// <summary>
    /// Affected process ID
    /// </summary>
    public string? ProcessId { get; set; }

    /// <summary>
    /// Affected asset ID
    /// </summary>
    public string? AssetId { get; set; }

    /// <summary>
    /// Reported by user ID
    /// </summary>
    public string? ReportedById { get; set; }

    /// <summary>
    /// Assigned to user ID
    /// </summary>
    public string? AssignedToId { get; set; }

    /// <summary>
    /// Assigned to organization unit ID
    /// </summary>
    public int? AssignedToUnitId { get; set; }

    /// <summary>
    /// Related problem ID (if linked to a problem)
    /// </summary>
    public string? ProblemId { get; set; }

    /// <summary>
    /// Date and time incident was reported
    /// </summary>
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time incident was acknowledged
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Date and time incident was resolved
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Date and time incident was closed
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// SLA target resolution time (in hours)
    /// </summary>
    public int SlaTargetHours { get; set; } = 24;

    /// <summary>
    /// SLA due date/time
    /// </summary>
    public DateTime SlaDueDate { get; set; }

    /// <summary>
    /// Whether SLA was breached
    /// </summary>
    public bool SlaBreached { get; set; } = false;

    /// <summary>
    /// Resolution notes
    /// </summary>
    public string? ResolutionNotes { get; set; }

    /// <summary>
    /// Workaround applied
    /// </summary>
    public string? Workaround { get; set; }

    /// <summary>
    /// Root cause (if identified)
    /// </summary>
    public string? RootCause { get; set; }

    /// <summary>
    /// Customer satisfaction rating (1-5)
    /// </summary>
    [Range(1, 5)]
    public int? SatisfactionRating { get; set; }

    /// <summary>
    /// Customer feedback
    /// </summary>
    public string? CustomerFeedback { get; set; }

    // Navigation properties
    public Service? Service { get; set; }
    public Process? Process { get; set; }
    public Asset? Asset { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? ReportedBy { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? AssignedTo { get; set; }
    public OrganizationUnit? AssignedToUnit { get; set; }
    public Problem? Problem { get; set; }
    public ICollection<IncidentComment> Comments { get; set; } = new List<IncidentComment>();

    /// <summary>
    /// Calculate SLA due date based on priority
    /// </summary>
    public void CalculateSlaDueDate()
    {
        SlaTargetHours = Priority switch
        {
            1 => 4,   // Critical: 4 hours
            2 => 8,   // High: 8 hours
            3 => 24,  // Medium: 24 hours
            4 => 72,  // Low: 72 hours
            _ => 24
        };
        SlaDueDate = ReportedAt.AddHours(SlaTargetHours);
    }

    /// <summary>
    /// Check if SLA is breached
    /// </summary>
    public void CheckSlaStatus()
    {
        if (Status != IncidentStatus.Resolved && Status != IncidentStatus.Closed)
        {
            SlaBreached = DateTime.UtcNow > SlaDueDate;
        }
    }
}

