using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.AssetManagement;

namespace ESEMS.Web.Models.ServiceManagement;

/// <summary>
/// Represents a problem (root cause of incidents) in the IT Service Management system (ISO 20000-1:2018)
/// </summary>
public class Problem : AuditableBilingualEntity, Common.IAssignedToUnit
{
    /// <summary>
    /// Problem number (auto-generated, e.g., PRB-2026-0001)
    /// </summary>
    public string ProblemNumber { get; set; } = string.Empty;

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
    /// Current status
    /// </summary>
    public ProblemStatus Status { get; set; } = ProblemStatus.New;

    /// <summary>
    /// Category of the problem
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
    /// Problem owner/manager user ID
    /// </summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// Assigned to organization unit ID
    /// </summary>
    public int? AssignedToUnitId { get; set; }

    /// <summary>
    /// Date and time problem was identified
    /// </summary>
    public DateTime IdentifiedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Date and time root cause was identified
    /// </summary>
    public DateTime? RootCauseIdentifiedAt { get; set; }

    /// <summary>
    /// Date and time problem was resolved
    /// </summary>
    public DateTime? ResolvedAt { get; set; }

    /// <summary>
    /// Date and time problem was closed
    /// </summary>
    public DateTime? ClosedAt { get; set; }

    /// <summary>
    /// Root cause analysis
    /// </summary>
    public string? RootCauseAnalysis { get; set; }

    /// <summary>
    /// Workaround description
    /// </summary>
    public string? Workaround { get; set; }

    /// <summary>
    /// Permanent solution description
    /// </summary>
    public string? PermanentSolution { get; set; }

    /// <summary>
    /// Known error (documented in knowledge base)
    /// </summary>
    public bool IsKnownError { get; set; } = false;

    /// <summary>
    /// Knowledge base article ID
    /// </summary>
    public string? KnowledgeBaseArticleId { get; set; }

    /// <summary>
    /// Number of related incidents
    /// </summary>
    public int RelatedIncidentCount { get; set; } = 0;

    /// <summary>
    /// Estimated cost impact
    /// </summary>
    public decimal? EstimatedCostImpact { get; set; }

    /// <summary>
    /// Actual cost impact
    /// </summary>
    public decimal? ActualCostImpact { get; set; }

    // Navigation properties
    public Service? Service { get; set; }
    public Process? Process { get; set; }
    public Asset? Asset { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? Owner { get; set; }
    public OrganizationUnit? AssignedToUnit { get; set; }
    public ICollection<Incident> RelatedIncidents { get; set; } = new List<Incident>();
    public ICollection<ProblemComment> Comments { get; set; } = new List<ProblemComment>();

    /// <summary>
    /// Update related incident count
    /// </summary>
    public void UpdateIncidentCount()
    {
        RelatedIncidentCount = RelatedIncidents?.Count ?? 0;
    }
}

/// <summary>
/// Comment on an incident
/// </summary>
public class IncidentComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string IncidentId { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsInternal { get; set; } = false;

    // Navigation properties
    public Incident? Incident { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? CreatedBy { get; set; }
}

/// <summary>
/// Comment on a problem
/// </summary>
public class ProblemComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProblemId { get; set; } = string.Empty;
    public string Comment { get; set; } = string.Empty;
    public string? CreatedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsInternal { get; set; } = false;

    // Navigation properties
    public Problem? Problem { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? CreatedBy { get; set; }
}

