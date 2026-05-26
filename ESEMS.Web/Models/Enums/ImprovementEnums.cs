namespace ESEMS.Web.Models.Enums;

/// <summary>
/// Lifecycle status for an <see cref="Improvement.ImprovementInitiative"/>.
/// Mirrors the string constants previously held in
/// <c>ImprovementStatusMachine</c>; promoted to a real enum so EF and the
/// state machine are type-safe end-to-end (audit finding #4).
///
/// Persisted as the enum *name* (nvarchar) via a value converter so existing
/// rows ("Proposed", "UnderReview", ...) keep round-tripping unchanged.
/// </summary>
public enum ImprovementStatus
{
    Proposed,
    UnderReview,
    Approved,
    InProgress,
    OnHold,
    Completed,
    Closed,

    /// <summary>
    /// Audit #1 — DGEP 4G Strategic Management §2.4 mandates explicit
    /// post-closure benefits-realisation tracking at 3M / 6M / 12M cadence.
    /// Initiative enters this state from <see cref="Closed"/> on the first
    /// scheduled review and stays here until <see cref="Sustained"/> is
    /// reached (12M review passed) or <see cref="Cancelled"/>.
    /// </summary>
    BenefitsRealization,

    /// <summary>
    /// Audit #1 — terminal state reached after the 12M benefits review
    /// confirms the actual benefits were realised. Distinct from
    /// <see cref="Closed"/> so the executive dashboard can distinguish
    /// "delivered" from "delivered AND proven sustained".
    /// </summary>
    Sustained,

    Rejected,
    Cancelled
}

/// <summary>
/// Lifecycle status for an <see cref="Improvement.ImprovementAction"/>
/// (audit finding #11). Persisted as the enum name via a value converter.
/// </summary>
public enum ImprovementActionStatus
{
    Pending,
    InProgress,
    Blocked,
    Completed,
    Cancelled
}

/// <summary>
/// Direction of improvement for an <see cref="Improvement.ImprovementMeasurement"/>
/// (audit finding #19). HigherBetter = increase is good (e.g. satisfaction %),
/// LowerBetter = decrease is good (e.g. processing time, error rate).
/// Persisted as the enum name via a value converter.
/// </summary>
public enum MeasurementDirection
{
    HigherBetter,
    LowerBetter
}
