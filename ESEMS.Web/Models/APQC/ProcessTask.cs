using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Services;
using System.ComponentModel.DataAnnotations;

namespace ESEMS.Web.Models.APQC;

/// <summary>
/// APQC Level 5 - Task (Optional)
/// Smallest unit of work within an activity
/// </summary>
public class ProcessTask : MeasurableEntity
{
    /// <summary>
    /// Task code (e.g., "1.1.1.1.1", "1.1.1.1.2")
    /// </summary>
    [Required]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Parent activity ID
    /// </summary>
    [Required]
    public string ActivityId { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Channel type (Digital, Physical, Hybrid)
    /// REQUIRED: Every task must specify delivery channel
    /// </summary>
    [Required]
    public ChannelType ChannelType { get; set; } = ChannelType.Hybrid;

    /// <summary>
    /// Owning organizational unit ID. Nullable since the org-unit merge reset
    /// all unit links — tasks are re-assigned to an owner after re-import.
    /// </summary>
    public int? OwningUnitId { get; set; }

    /// <summary>
    /// Multiple configurable tags (comma-separated)
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// Linked system definition ID
    /// </summary>
    public string? SystemId { get; set; }

    // ======== New Procedure-specific properties (MBRHE Process Catalog) ========

    /// <summary>
    /// Procedure status (Approved, Cancelled, UnderReview, Draft)
    /// Maps to "حالة الإجراء" column in Excel
    /// REQUIRED: Governance requirement for approval tracking
    /// </summary>
    [Required]
    public ProcedureStatus ProcedureStatus { get; set; } = ProcedureStatus.Draft;

    /// <summary>
    /// Automation status (Traditional, SemiAutomated, Automated)
    /// Maps to "الأتمتة" column in Excel
    /// REQUIRED: Critical for digital transformation KPIs
    /// </summary>
    [Required]
    public AutomationStatus AutomationStatus { get; set; } = AutomationStatus.Traditional;

    /// <summary>
    /// Name of the digital system used
    /// Maps to "النظام التقني" column in Excel
    /// </summary>
    public string? DigitalSystemName { get; set; }

    /// <summary>
    /// Automability assessment
    /// Maps to "قابل/غير قابل للأتمتة" column in Excel
    /// </summary>
    public AutomabilityStatus AutomabilityStatus { get; set; } = AutomabilityStatus.NotAutomatable;

    /// <summary>
    /// Current or Proposed status
    /// Maps to "وضع حالي/مقترح" column in Excel
    /// </summary>
    public CurrentProposedStatus CurrentProposedStatus { get; set; } = CurrentProposedStatus.Current;

    /// <summary>
    /// Automation assessment scores (JSON format)
    /// Contains: Priority (داعم/رئيسي), Readiness (مؤتمتة/شبه مؤتمتة/تقليدية),
    /// LinkageAssessment (تقييم الارتباط), AutomationPriority (أولوية الأتمتة), CompositeScore
    /// </summary>
    public string? AutomationAssessmentScores { get; set; }

    /// <summary>
    /// Linked services (comma-separated)
    /// Maps to "اسم الخدمة الفرعية المرتبطة بالعملية" column in Excel
    /// </summary>
    public string? LinkedServices { get; set; }

    /// <summary>
    /// Document reference code
    /// Maps to "التمييز الحالي" column in Excel (e.g., "MBRHE-ECS-QM-PR01-AR")
    /// </summary>
    public string? DocumentReference { get; set; }

    /// <summary>
    /// Document language
    /// Maps to "لغة الوثيقة" column in Excel
    /// </summary>
    public string? DocumentLanguage { get; set; }

    /// <summary>
    /// BPMN 2.0 diagram XML content
    /// Contains the Visio diagram converted to BPMN format
    /// </summary>
    public string? BpmnDiagram { get; set; }

    // Navigation properties
    public Activity? Activity { get; set; }
    public OrganizationUnit? OwningUnit { get; set; }
    public SystemDefinition? System { get; set; }
    public ICollection<TaskRaci> RaciMatrix { get; set; } = new List<TaskRaci>();

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

