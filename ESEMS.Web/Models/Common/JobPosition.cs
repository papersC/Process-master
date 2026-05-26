using System.ComponentModel.DataAnnotations.Schema;
using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models.Common;

/// <summary>
/// Org-wide catalog of job positions — Director / Deputy Director / Section Head /
/// Senior Specialist / Specialist / Coordinator / Reviewer / Officer / etc.
/// Distinct from <see cref="RoleGroup"/> (which is a security/permissions bundle
/// for access control). A JobPosition is the functional position a person holds:
/// "the Director", "the Quality Reviewer", "the Coordinator".
///
/// Why org-wide and not unit-scoped: at MBRHE, a "Director" is a "Director"
/// regardless of which unit they head. The position combines with an
/// <see cref="APQC.OrganizationUnit"/> to identify a specific role-in-unit
/// (e.g. "Director of إدارة المشاريع الهندسية"). Keeping the catalog flat
/// avoids 200 near-identical "Director of X" rows.
///
/// Used as the FK target for the new <c>RaciBase.JobPositionId</c> field —
/// RACI rows can now point at (Unit, JobPosition) instead of just Unit, the
/// APQC / ISO 9001 §5.3 conformant form. JobPositionId stays nullable so
/// pre-existing unit-only RACI rows keep working.
///
/// The DB table is still named <c>JobRoles</c> (historical) — preserved via
/// [Table] so this rename is C#-only and needs no column-rename migration.
/// </summary>
[Table("JobRoles")]
public class JobPosition : AuditableBilingualEntity
{
    /// <summary>
    /// Short admin code — "DIR", "DEP-DIR", "SEC-HEAD", "SR-SPEC". Optional
    /// but useful for import scripts and external references.
    /// </summary>
    public string? Code { get; set; }

    /// <summary>
    /// Grouping for the catalog UI — "Leadership" / "Specialist" /
    /// "Administrative" / "Technical". Free text, optional.
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Marks director-level positions. Used by the RACI editor to default
    /// "Accountable" picks to leadership positions and by permission filters
    /// that need to know who can approve at unit level.
    /// </summary>
    public bool IsLeadership { get; set; }

    /// <summary>
    /// Display order across the catalog. Lower values render first.
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Optional unit scope. NULL = global position (Director, Specialist,
    /// etc. — available everywhere). Set = unit-specific position visible
    /// only when the parent entity (Process / Activity / Task) is owned by
    /// this unit. Lets MBRHE define "Director of Communication Section" as a
    /// distinct position from the global "Director" if the granularity
    /// matters.
    /// </summary>
    public int? OrganizationUnitId { get; set; }
    public OrganizationUnit? OrganizationUnit { get; set; }
}
