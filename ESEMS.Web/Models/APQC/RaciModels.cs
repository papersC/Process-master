using System.ComponentModel.DataAnnotations.Schema;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.APQC;

/// <summary>
/// Base class for RACI matrix entries.
///
/// RACI rows are scoped to a unit (always) and optionally to a specific
/// <see cref="JobPosition"/> within that unit (the APQC / ISO 9001 §5.3
/// conformant form). When <see cref="JobPositionId"/> is NULL the row applies
/// to the unit as a whole — the simpler coarse-grained form, preserved for
/// backward compatibility with rows that pre-date the position catalog.
///
/// Field name <c>Role</c> on this class means the RACI letter (R/A/C/I), not
/// the job position. Two distinct concepts, one unfortunate word.
/// </summary>
public abstract class RaciBase
{
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Organization unit ID
    /// </summary>
    public int OrganizationUnitId { get; set; }

    /// <summary>
    /// Optional FK to <see cref="Common.JobPosition"/> — specifies *which
    /// position* within the unit this RACI row applies to (e.g. "the
    /// Director" of إدارة المشاريع الهندسية). NULL means "the unit as a
    /// whole" — legacy / coarse RACI.
    ///
    /// DB column is still named <c>JobRoleId</c> (historical) — preserved via
    /// [Column] so this rename is C#-only and needs no column-rename
    /// migration.
    /// </summary>
    [Column("JobRoleId")]
    public string? JobPositionId { get; set; }

    /// <summary>
    /// RACI letter (Responsible, Accountable, Consulted, Informed).
    /// Property name is historical — predates the position catalog.
    /// </summary>
    public RACIRole Role { get; set; }

    /// <summary>
    /// Additional notes
    /// </summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public OrganizationUnit? OrganizationUnit { get; set; }
    public JobPosition? JobPosition { get; set; }
}

/// <summary>
/// RACI matrix entry for Process (Level 3)
/// </summary>
public class ProcessRaci : RaciBase
{
    public string ProcessId { get; set; } = string.Empty;
    public Process? Process { get; set; }
}

/// <summary>
/// RACI matrix entry for Activity (Level 4)
/// </summary>
public class ActivityRaci : RaciBase
{
    public string ActivityId { get; set; } = string.Empty;
    public Activity? Activity { get; set; }
}

/// <summary>
/// RACI matrix entry for Task (Level 5)
/// </summary>
public class TaskRaci : RaciBase
{
    public string TaskId { get; set; } = string.Empty;
    public ProcessTask? Task { get; set; }
}
