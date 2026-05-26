using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Audit #15 — central KPI catalog (DGEP 4G Performance Measurement §5.1).
///
/// Solves the "every measurement is free text" problem the audit flagged:
/// across 50+ initiatives the same metric ("CSAT", "Customer Satisfaction",
/// "نسبة الرضا", "Cust Sat %") was being entered as four different rows
/// with four different units, making cross-initiative aggregation
/// impossible.
///
/// Each <see cref="ImprovementMeasurement"/> can optionally point at a
/// <see cref="KpiDefinition"/> via <see cref="ImprovementMeasurement.KpiDefinitionId"/>.
/// When linked, the measurement inherits the canonical name, unit, and
/// direction from the catalog — so the dashboard can group by definition,
/// not by free-text name.
///
/// Table is intentionally seeded empty in this commit. PMO populates it
/// from the admin UI (Improvements/KpiLibrary). Backfilling existing
/// measurements onto definitions is a future tweak — for now it's purely
/// opt-in for new measurements.
/// </summary>
[Table("KpiDefinitions")]
public class KpiDefinition : AuditableBilingualEntity
{
    /// <summary>
    /// Stable code (e.g. "CSAT", "PROC_TIME"). Unique across the catalog
    /// so reports can reference KPIs by code without ambiguity.
    /// </summary>
    [Required, MaxLength(50)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Canonical unit (e.g. "%", "minutes", "AED", "transactions").
    /// </summary>
    [Required, MaxLength(50)]
    public string UnitOfMeasure { get; set; } = string.Empty;

    /// <summary>
    /// Default direction for this KPI — used to seed
    /// <see cref="ImprovementMeasurement.Direction"/> when a measurement
    /// is created from this definition.
    /// </summary>
    public MeasurementDirection Direction { get; set; } = MeasurementDirection.HigherBetter;

    /// <summary>
    /// Canonical measurement type (Cost / Time / Satisfaction / etc.).
    /// </summary>
    public ImprovementMeasurementType DefaultType { get; set; } = ImprovementMeasurementType.Custom;

    /// <summary>
    /// Owning unit responsible for the KPI definition (the "data steward").
    /// FK is loose — KPIs are often shared across the entity.
    /// </summary>
    public int? OwningUnitId { get; set; }

    /// <summary>
    /// Whether new measurements should still be allowed to reference this
    /// definition. Setting false retires the KPI from the picker without
    /// breaking historical references.
    /// </summary>
    public bool IsActive { get; set; } = true;
}
