using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.AssetManagement;
using ESEMS.Web.Models.RiskManagement;
using ESEMS.Web.Models.ServiceManagement;
using ESEMS.Web.Models.DocumentManagement;

namespace ESEMS.Web.Models.APQC;

/// <summary>
/// APQC Level 3 - Process
/// End-to-end business process
/// </summary>
public class Process : MeasurableEntity, IOwnedByUnit
{
    /// <summary>
    /// Process code — "{ProcessGroup.Code}.{Z}" in the hierarchical scheme
    /// (e.g. "1.1.1", "1.1.2"). Auto-generated; not user-editable.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Original code from before the hierarchical-code migration
    /// (e.g. "PRC-0024", "MP-001", "SP-002").
    /// </summary>
    public string? LegacyCode { get; set; }

    /// <summary>
    /// Zero-padded Code for natural sort (e.g. "0001.0002.0003").
    /// </summary>
    public string? SortKey { get; set; }

    /// <summary>
    /// Optional parent process — populated for legacy SP-* sub-processes
    /// pointing at their MP-* main process. Replaces the old MP-/SP- prefix
    /// convention now that codes are hierarchical and prefix-free.
    /// </summary>
    public string? ParentProcessId { get; set; }

    /// <summary>
    /// Parent process group ID
    /// </summary>
    public string ProcessGroupId { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting (legacy — prefer SortKey).
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Process type (Core, Support, Management)
    /// </summary>
    public ProcessType ProcessType { get; set; } = ProcessType.Core;

    /// <summary>
    /// Current status of the process
    /// </summary>
    public ProcessStatus Status { get; set; } = ProcessStatus.Draft;

    /// <summary>
    /// Owning organizational unit ID
    /// </summary>
    public int? OwningUnitId { get; set; }

    /// <summary>
    /// Multiple configurable tags (comma-separated)
    /// </summary>
    public string? Tags { get; set; }

    /// <summary>
    /// BPMN 2.0 diagram XML content
    /// </summary>
    public string? BpmnDiagram { get; set; }

    /// <summary>
    /// BPMN diagram file path
    /// </summary>
    public string? BpmnFilePath { get; set; }

    /// <summary>
    /// Linked strategic objective ID
    /// </summary>
    public string? StrategicObjectiveId { get; set; }

    /// <summary>
    /// Linked service ID (DEPRECATED - Use ProcessServices collection instead)
    /// Kept for backward compatibility during migration
    /// </summary>
    [Obsolete("Use ProcessServices collection for many-to-many relationship")]
    public string? ServiceId { get; set; }

    /// <summary>
    /// Linked system definition ID
    /// </summary>
    public string? SystemId { get; set; }

    /// <summary>
    /// Aggregated total duration in minutes from child activities
    /// </summary>
    public decimal? AggregatedDurationMinutes { get; set; }

    /// <summary>
    /// Aggregated total cost from child activities
    /// </summary>
    public decimal? AggregatedCost { get; set; }

    /// <summary>
    /// Whether this process has optional Level 4/5 breakdown
    /// </summary>
    public bool HasDetailedBreakdown { get; set; }

    // ======== New properties based on MBRHE Process Catalog ========

    /// <summary>
    /// Automation status (Traditional, SemiAutomated, Automated)
    /// </summary>
    public AutomationStatus AutomationStatus { get; set; } = AutomationStatus.Traditional;

    /// <summary>
    /// Name of the digital system used (e.g., "CRM", "Oracle", "GRP")
    /// </summary>
    public string? DigitalSystemName { get; set; }

    /// <summary>
    /// Automability assessment (whether the process can be automated)
    /// </summary>
    public AutomabilityStatus AutomabilityStatus { get; set; } = AutomabilityStatus.NotAutomatable;

    /// <summary>
    /// Process classification type (Main, Support, Enabling)
    /// </summary>
    public ProcessClassificationType ClassificationType { get; set; } = ProcessClassificationType.Main;

    /// <summary>
    /// Current or Proposed status for process changes
    /// </summary>
    public CurrentProposedStatus CurrentProposedStatus { get; set; } = CurrentProposedStatus.Current;

    /// <summary>
    /// Linked services (comma-separated service names)
    /// </summary>
    public string? LinkedServices { get; set; }

    /// <summary>
    /// External partners involved (comma-separated)
    /// </summary>
    public string? ExternalPartners { get; set; }

    /// <summary>
    /// Government partners linked to the process (comma-separated)
    /// </summary>
    public string? GovernmentPartners { get; set; }

    /// <summary>
    /// Projects and initiatives linked to this process (comma-separated)
    /// </summary>
    public string? LinkedProjects { get; set; }

    /// <summary>
    /// Mandate/Responsibilities link (Links to organizational mandate)
    /// </summary>
    public string? MandateResponsibilities { get; set; }

    /// <summary>
    /// Document reference code (e.g., "MBRHE-ECS-QM-PR01-AR")
    /// </summary>
    public string? DocumentReference { get; set; }

    /// <summary>
    /// Document language (Arabic, English, Bilingual).
    /// Stored as a string for backwards compatibility but restricted to the values of
    /// <see cref="Enums.DocumentLanguage"/> in the UI.
    /// </summary>
    public string? DocumentLanguage { get; set; }

    /// <summary>
    /// Document classification category (Information Security, Business Continuity, etc.).
    /// FK to <see cref="DocumentManagement.DocumentCategory"/>.
    /// </summary>
    public string? DocumentCategoryId { get; set; }

    /// <summary>
    /// Document type (Policy, Procedure, Standard, Plan, etc.).
    /// FK to <see cref="DocumentManagement.DocumentType"/>.
    /// </summary>
    public string? DocumentTypeId { get; set; }

    /// <summary>
    /// Automation assessment scores (JSON or comma-separated values)
    /// Contains: Priority, Readiness, LinkageAssessment, AutomationPriority, CompositeScore
    /// </summary>
    public string? AutomationAssessmentScores { get; set; }

    /// <summary>
    /// Tasks/responsibilities description for this process
    /// </summary>
    public string? Tasks { get; set; }

    // Navigation properties
    public ProcessGroup? ProcessGroup { get; set; }
    public OrganizationUnit? OwningUnit { get; set; }
    public StrategicObjective? StrategicObjective { get; set; }

    /// <summary>
    /// Single service navigation (DEPRECATED - Use ProcessServices collection instead)
    /// </summary>
    [Obsolete("Use ProcessServices collection for many-to-many relationship")]
    public Service? Service { get; set; }

    public SystemDefinition? System { get; set; }
    public DocumentCategory? DocumentCategory { get; set; }
    public DocumentType? DocumentType { get; set; }
    public ICollection<Activity> Activities { get; set; } = new List<Activity>();
    public ICollection<ProcessRaci> RaciMatrix { get; set; } = new List<ProcessRaci>();
    public ICollection<ProcessRisk> Risks { get; set; } = new List<ProcessRisk>();
    public ICollection<ProcessMeasurement> Measurements { get; set; } = new List<ProcessMeasurement>();

    /// <summary>
    /// Many-to-many relationship with Services
    /// </summary>
    public ICollection<ProcessService> ProcessServices { get; set; } = new List<ProcessService>();

    /// <summary>
    /// Many-to-many relationship with Strategic Objectives
    /// </summary>
    public ICollection<ProcessStrategicObjective> ProcessStrategicObjectives { get; set; } = new List<ProcessStrategicObjective>();

    /// <summary>
    /// Many-to-many — chartered OrganizationUnit responsibilities this process fulfills.
    /// </summary>
    public ICollection<ProcessResponsibility> ProcessResponsibilities { get; set; } = new List<ProcessResponsibility>();

    // Reverse navigation properties (Process as parent)
    public ICollection<Asset> Assets { get; set; } = new List<Asset>();
    public ICollection<EnterpriseRisk> EnterpriseRisks { get; set; } = new List<EnterpriseRisk>();
    public ICollection<Incident> Incidents { get; set; } = new List<Incident>();
    public ICollection<Problem> Problems { get; set; } = new List<Problem>();

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

