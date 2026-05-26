using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models.Feedback;

/// <summary>
/// Represents a feedback category (ISO 9001:2015)
/// </summary>
public class FeedbackCategory : AuditableBilingualEntity
{
    /// <summary>
    /// Category code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Parent category ID (for hierarchical categorization)
    /// </summary>
    public string? ParentCategoryId { get; set; }

    /// <summary>
    /// Default priority (1=Critical, 2=High, 3=Medium, 4=Low)
    /// </summary>
    public int? DefaultPriority { get; set; }

    /// <summary>
    /// Default assigned to unit ID
    /// </summary>
    public int? DefaultAssignedToUnitId { get; set; }

    /// <summary>
    /// Expected response time (in hours)
    /// </summary>
    public int? ExpectedResponseTimeHours { get; set; }

    // Navigation properties
    public FeedbackCategory? ParentCategory { get; set; }
    public OrganizationUnit? DefaultAssignedToUnit { get; set; }
    public ICollection<FeedbackCategory> SubCategories { get; set; } = new List<FeedbackCategory>();
    public ICollection<CustomerFeedback> Feedbacks { get; set; } = new List<CustomerFeedback>();
}

