using System.ComponentModel.DataAnnotations;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Per-initiative immutable change log (audit #9). Captures every property
/// change on an <see cref="ImprovementInitiative"/> — old value, new value,
/// who, when, and (optionally) why.
///
/// Populated by <see cref="ESEMS.Web.Data.ImprovementChangeLogInterceptor"/>
/// which hooks <c>SaveChangesAsync</c> on the DbContext. The global AuditLog
/// table answers "who touched what entity"; this table answers "what
/// specifically about this initiative changed and to what" — the difference
/// DGEP 4G Audit & Accountability §A.12.4 requires.
/// </summary>
public class ImprovementChangeLog
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>FK to <see cref="ImprovementInitiative.Id"/>.</summary>
    [Required, MaxLength(450)]
    public string ImprovementId { get; set; } = string.Empty;

    /// <summary>EF entity property name (e.g. "Status", "OwnerId").</summary>
    [Required, MaxLength(100)]
    public string FieldName { get; set; } = string.Empty;

    /// <summary>String form of the previous value (null if new entity).</summary>
    [MaxLength(2000)]
    public string? OldValue { get; set; }

    /// <summary>String form of the new value (null if cleared / soft-deleted).</summary>
    [MaxLength(2000)]
    public string? NewValue { get; set; }

    /// <summary>UserId / UserName of the principal who triggered the change.</summary>
    [MaxLength(150)]
    public string? ChangedById { get; set; }

    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional human reason. Today the interceptor only captures it for
    /// status transitions (e.g. "Approved by sponsor — comments: …"). Other
    /// edits leave this null. Hook future actions (Submit / Reject / Close)
    /// to set it via a context-flow property.
    /// </summary>
    [MaxLength(500)]
    public string? ChangeReason { get; set; }

    // Navigation
    public ImprovementInitiative? Improvement { get; set; }
}
