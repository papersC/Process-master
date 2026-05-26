using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Services;

namespace ESEMS.Web.Models.Feedback;

/// <summary>
/// Represents customer feedback (ISO 9001:2015)
/// </summary>
public class CustomerFeedback : AuditableBilingualEntity
{
    /// <summary>
    /// Feedback number (auto-generated, e.g., FB-2026-0001)
    /// </summary>
    public string FeedbackNumber { get; set; } = string.Empty;

    /// <summary>
    /// Feedback type
    /// </summary>
    public FeedbackType Type { get; set; } = FeedbackType.Suggestion;

    /// <summary>
    /// Feedback category ID
    /// </summary>
    public string? CategoryId { get; set; }

    /// <summary>
    /// Related service ID
    /// </summary>
    public string? ServiceId { get; set; }

    /// <summary>
    /// Related process ID
    /// </summary>
    public string? ProcessId { get; set; }

    /// <summary>
    /// Customer name
    /// </summary>
    public string CustomerName { get; set; } = string.Empty;

    /// <summary>
    /// Customer email
    /// </summary>
    public string? CustomerEmail { get; set; }

    /// <summary>
    /// Customer phone
    /// </summary>
    public string? CustomerPhone { get; set; }

    /// <summary>
    /// Organization/company name
    /// </summary>
    public string? OrganizationName { get; set; }

    /// <summary>
    /// Submitted date
    /// </summary>
    public DateTime SubmittedDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Current status
    /// </summary>
    public FeedbackStatus Status { get; set; } = FeedbackStatus.New;

    /// <summary>
    /// Priority (1=Critical, 2=High, 3=Medium, 4=Low)
    /// </summary>
    [Range(1, 4)]
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Assigned to user ID
    /// </summary>
    public string? AssignedToId { get; set; }

    /// <summary>
    /// Assigned to organization unit ID
    /// </summary>
    public int? AssignedToUnitId { get; set; }

    /// <summary>
    /// Response
    /// </summary>
    public string? Response { get; set; }

    /// <summary>
    /// Response date
    /// </summary>
    public DateTime? ResponseDate { get; set; }

    /// <summary>
    /// Resolution date
    /// </summary>
    public DateTime? ResolutionDate { get; set; }

    /// <summary>
    /// Resolution notes
    /// </summary>
    public string? ResolutionNotes { get; set; }

    /// <summary>
    /// Customer satisfaction rating (1-5)
    /// </summary>
    [Range(1, 5)]
    public int? SatisfactionRating { get; set; }

    /// <summary>
    /// Follow-up required
    /// </summary>
    public bool RequiresFollowUp { get; set; } = false;

    /// <summary>
    /// Follow-up date
    /// </summary>
    public DateTime? FollowUpDate { get; set; }

    /// <summary>
    /// Root cause (if applicable)
    /// </summary>
    public string? RootCause { get; set; }

    /// <summary>
    /// Corrective action taken
    /// </summary>
    public string? CorrectiveAction { get; set; }

    /// <summary>
    /// Preventive action taken
    /// </summary>
    public string? PreventiveAction { get; set; }

    // Navigation properties
    public FeedbackCategory? Category { get; set; }
    public Service? Service { get; set; }
    public Process? Process { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? AssignedTo { get; set; }
    public OrganizationUnit? AssignedToUnit { get; set; }

    /// <summary>
    /// Check if feedback is overdue for response
    /// </summary>
    public bool IsOverdueForResponse()
    {
        // Response expected within 2 business days for complaints, 5 days for others
        var expectedResponseDays = Type == FeedbackType.Complaint ? 2 : 5;
        var dueDate = SubmittedDate.AddDays(expectedResponseDays);
        
        return !ResponseDate.HasValue && DateTime.UtcNow > dueDate;
    }

    /// <summary>
    /// Check if follow-up is due
    /// </summary>
    public bool IsFollowUpDue()
    {
        return RequiresFollowUp && 
               FollowUpDate.HasValue && 
               FollowUpDate.Value <= DateTime.UtcNow;
    }
}

