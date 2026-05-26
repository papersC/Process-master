using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.APQC;

/// <summary>
/// One row per <c>&lt;bpmn:lane&gt;</c> parsed out of a Process's BPMN diagram.
/// The whole point of this entity is to reconcile BPMN swimlane names with
/// the OrganizationUnit hierarchy — so when a manager asks "which unit owns
/// this activity," the answer is more than "look inside the XML blob."
///
/// Reconciliation strategy at import time (see <see cref="Services.Bpmn.BpmnLaneReconciler"/>):
///   1. exact name match against OrganizationUnit.NameEn / NameAr (case-insensitive)
///   2. trimmed + punctuation-stripped match
///   3. if still nothing and <c>autoCreate</c> is on, create a new unit
///      under a synthetic "BPMN Import" root
///   4. otherwise leave <see cref="OrganizationUnitId"/> null and add to the
///      review queue
///
/// Activities whose <c>&lt;bpmn:flowNodeRef&gt;</c> lives inside this lane
/// get their <c>OwningUnitId</c> backfilled to the lane's resolved unit.
/// </summary>
public class BpmnLane
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    public string ProcessId { get; set; } = string.Empty;

    /// <summary>The lane's <c>id</c> attribute in the BPMN XML.</summary>
    [Required, MaxLength(256)]
    public string BpmnId { get; set; } = string.Empty;

    /// <summary>Lane name as it appears in the source BPMN (whatever the modeller typed).</summary>
    [Required, MaxLength(512)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Resolved OrganizationUnit. Null when the lane name didn't match any
    /// unit and autoCreate wasn't on — the lane stays in the review queue
    /// until someone maps it manually.
    /// </summary>
    public int? OrganizationUnitId { get; set; }

    /// <summary>How the resolution happened. Used by the review UI to surface low-confidence matches.</summary>
    [MaxLength(32)]
    public string MatchMethod { get; set; } = MatchMethods.Pending;

    /// <summary>When the unit link was last set (auto or manual).</summary>
    public DateTime? MatchedAt { get; set; }

    /// <summary>User who manually mapped the lane (null when auto-resolved).</summary>
    [MaxLength(256)]
    public string? MatchedById { get; set; }

    /// <summary>JSON-encoded list of BPMN flowNode IDs that belong to this lane (informational).</summary>
    public string? FlowNodeRefsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    [ForeignKey(nameof(ProcessId))]
    public Process? Process { get; set; }

    [ForeignKey(nameof(OrganizationUnitId))]
    public OrganizationUnit? OrganizationUnit { get; set; }

    public static class MatchMethods
    {
        public const string Pending = "Pending";          // not yet reconciled
        public const string Exact = "Exact";              // case-insensitive name equality
        public const string Normalized = "Normalized";    // punctuation/whitespace stripped match
        public const string AutoCreated = "AutoCreated";  // new OrgUnit created during import
        public const string Manual = "Manual";            // someone clicked "map" in the review UI
        public const string Ignored = "Ignored";          // operator decided this lane isn't an org unit
    }
}
