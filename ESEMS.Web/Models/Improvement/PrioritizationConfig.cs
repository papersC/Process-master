using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Tunable quadrant thresholds for the Improvements module (audit #17).
///
/// Replaces the hard-coded <c>ImpactScore &gt;= 6 / EffortScore &gt;= 6</c>
/// cut-offs that were embedded in <see cref="ImprovementInitiative.CalculateQuadrant"/>.
/// PMO can now retune the QuickWins / MajorProjects / FillIns / ThanklessTasks
/// boundaries per fiscal year (or per Pillar) without a code change.
///
/// The active config is selected by <c>IsActive = true</c>; if no row exists,
/// the entity falls back to the original 6 / 6 cut-offs so legacy data still
/// renders correctly.
/// </summary>
[Table("PrioritizationConfigs")]
public class PrioritizationConfig
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Friendly name (e.g. "FY26 — MBRHE", "Default").
    /// </summary>
    [Required, MaxLength(150)]
    public string Name { get; set; } = "Default";

    /// <summary>
    /// Optional fiscal year tag for reporting; not used for lookup.
    /// </summary>
    public int? FiscalYear { get; set; }

    /// <summary>
    /// Score at or above which Impact is considered "high".
    /// Default 6 (range 1–10).
    /// </summary>
    [Range(1, 10)]
    public int ImpactCutoff { get; set; } = 6;

    /// <summary>
    /// Score at or above which Effort is considered "high".
    /// Default 6 (range 1–10).
    /// </summary>
    [Range(1, 10)]
    public int EffortCutoff { get; set; } = 6;

    /// <summary>
    /// Only one config should be IsActive at any time. The application picks
    /// the first IsActive row at startup; admins toggle from the config screen.
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Free-text notes for the council that approved these thresholds.
    /// </summary>
    [MaxLength(1000)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
