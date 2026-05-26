using System.ComponentModel.DataAnnotations;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.Enums;
using ESEMS.Web.Models.APQC;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Improvement initiative entity
/// </summary>
public class ImprovementInitiative : AuditableBilingualEntity, IOwnedByUnit
{
    /// <summary>
    /// Improvement code
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// Title in English
    /// </summary>
    [Required]
    [MinLength(3)]
    public string TitleEn { get; set; } = string.Empty;

    /// <summary>
    /// Title in Arabic
    /// </summary>
    [Required]
    [MinLength(3)]
    public string TitleAr { get; set; } = string.Empty;

    /// <summary>
    /// Display order for sorting
    /// </summary>
    public int DisplayOrder { get; set; }

    /// <summary>
    /// Audit #2: legacy single-FK column. Kept for backward compatibility
    /// with ~80 call sites that assume single linkage; the canonical source
    /// of truth is now <see cref="ImprovementProcesses"/> (M2M). The Batch B
    /// migration backfills the M2M from this column, so reports MUST query
    /// the M2M — querying ProcessId only will miss multi-linked initiatives.
    /// </summary>
    [Obsolete("Audit #2: use ImprovementProcesses (M2M) — the M2M is canonical. ProcessId remains as a deprecated single-link shim.")]
    public string? ProcessId { get; set; }

    /// <summary>
    /// Audit #2: legacy single-FK column. See <see cref="ProcessId"/> note.
    /// </summary>
    [Obsolete("Audit #2: use ImprovementServices (M2M) — the M2M is canonical. ServiceId remains as a deprecated single-link shim.")]
    public string? ServiceId { get; set; }

    /// <summary>
    /// Direct strategic anchor (audit #3). Previously the only path from an
    /// initiative to a strategic objective was indirect — via the linked
    /// Process or Service. DGEP 4G Pillar 1 requires every initiative carry
    /// an explicit strategic linkage so the portfolio dashboard can roll up
    /// to objective without inferring through derived joins.
    ///
    /// Nullable for one release so existing rows don't break; the controller
    /// blocks Submit-for-Approval until this FK is populated, which gives
    /// PMO a soft enforcement window before we can flip to NOT NULL.
    /// </summary>
    public string? StrategicObjectiveId { get; set; }

    /// <summary>
    /// Impact score (1-10). Server-side [Range] protects against direct
    /// POSTs that bypass the wizard's slider widget.
    /// </summary>
    [Range(1, 10)]
    public int ImpactScore { get; set; } = 5;

    /// <summary>
    /// Effort score (1-10).
    /// </summary>
    [Range(1, 10)]
    public int EffortScore { get; set; } = 5;

    /// <summary>
    /// Progress percentage (0-100). Bounded so the dashboard never shows
    /// "112% complete" because someone POSTed 112.
    /// </summary>
    [Range(0, 100)]
    public int ProgressPercentage { get; set; } = 0;

    /// <summary>
    /// Calculated quadrant based on impact and effort
    /// </summary>
    public ImprovementQuadrant Quadrant { get; set; } = ImprovementQuadrant.FillIns;

    /// <summary>
    /// Current lifecycle status. Type-safe enum (audit #4); persisted as
    /// nvarchar via a value converter in ApplicationDbContext so existing
    /// "Proposed"/"UnderReview"/... rows keep working unchanged.
    /// </summary>
    public ImprovementStatus Status { get; set; } = ImprovementStatus.Proposed;

    /// <summary>
    /// Priority — server-derived ranking, NOT a user-set 1-5 enum.
    /// Empirically the column carries values 1-11 in seeded data because
    /// ImprovementService bumps it from a combination of quadrant +
    /// prioritization score. Don't add [Range] here without first
    /// confirming the calculation never exceeds the cap, or the Edit
    /// form will refuse to save existing rows whose Priority was
    /// computed before the cap landed.
    /// </summary>
    public int Priority { get; set; } = 3;

    /// <summary>
    /// Estimated cost savings
    /// </summary>
    public decimal? EstimatedCostSavings { get; set; }

    /// <summary>
    /// Estimated time savings (in hours)
    /// </summary>
    public decimal? EstimatedTimeSavings { get; set; }

    /// <summary>
    /// Actual cost savings (after implementation)
    /// </summary>
    public decimal? ActualCostSavings { get; set; }

    /// <summary>
    /// Actual time savings (after implementation)
    /// </summary>
    public decimal? ActualTimeSavings { get; set; }

    /// <summary>
    /// Target completion date
    /// </summary>
    public DateTime? TargetDate { get; set; }

    /// <summary>
    /// Actual completion date
    /// </summary>
    public DateTime? CompletedDate { get; set; }

    /// <summary>
    /// Owner (User ID)
    /// </summary>
    public string? OwnerId { get; set; }

    /// <summary>
    /// Sponsor (User ID) — I12 audit finding. DGEP §2.2 Governance Roles
    /// requires explicit sponsor accountability beyond the inferred role
    /// in ImprovementTeamMember. Nullable for one release so existing
    /// rows don't break; the controller should block Submit-for-Approval
    /// until this FK is populated, mirroring StrategicObjectiveId's
    /// soft-enforcement window. The 12-month BenefitsReview sign-off
    /// MUST come from this user (or fail closure).
    /// </summary>
    public string? SponsorId { get; set; }

    /// <summary>
    /// Owning organizational unit ID
    /// </summary>
    public int? OwningUnitId { get; set; }

    /// <summary>
    /// Source of the improvement
    /// </summary>
    public ChangeRequestSource Source { get; set; } = ChangeRequestSource.InternalImprovement;

    /// <summary>
    /// External reference ID (e.g., from Innovation Management System)
    /// </summary>
    public string? ExternalReferenceId { get; set; }

    /// <summary>
    /// Innovation type classification (Draft8)
    /// </summary>
    public InnovationType? InnovationType { get; set; }

    /// <summary>
    /// Innovation horizon timeline (Draft8)
    /// </summary>
    public ImprovementHorizon? Horizon { get; set; }

    // ═══════════════════════════════════════════════════════════════
    // Prioritization Framework (9 weighted criteria from Draft8)
    // ═══════════════════════════════════════════════════════════════

    // Business Readiness (30%)
    /// <summary>Ease of Implementation score (1-10), Weight: 10%</summary>
    [Range(1, 10)]
    public int? EaseOfImplementation { get; set; }
    /// <summary>Budget Estimation score (1-10), Weight: 15%</summary>
    [Range(1, 10)]
    public int? BudgetEstimation { get; set; }
    /// <summary>Project Dependency score (1-10), Weight: 5%</summary>
    [Range(1, 10)]
    public int? ProjectDependency { get; set; }

    // Business Value (70%)
    /// <summary>Strategic Alignment score (1-10), Weight: 15%</summary>
    [Range(1, 10)]
    public int? StrategicAlignmentScore { get; set; }
    /// <summary>Leadership Directions score (1-10), Weight: 10%</summary>
    [Range(1, 10)]
    public int? LeadershipDirections { get; set; }
    /// <summary>Quality of Life score (1-10), Weight: 15%</summary>
    [Range(1, 10)]
    public int? QualityOfLife { get; set; }
    /// <summary>Innovation and Future Shaping score (1-10), Weight: 5%</summary>
    [Range(1, 10)]
    public int? InnovationAndFutureShaping { get; set; }
    /// <summary>Financial and Economic Impact score (1-10), Weight: 10%</summary>
    [Range(1, 10)]
    public int? FinancialAndEconomicImpact { get; set; }
    /// <summary>Sustainability score (1-10), Weight: 15%</summary>
    [Range(1, 10)]
    public int? SustainabilityScore { get; set; }

    /// <summary>Calculated Business Readiness score (weighted average)</summary>
    public decimal? BusinessReadinessScore =>
        (EaseOfImplementation.HasValue && BudgetEstimation.HasValue && ProjectDependency.HasValue)
        ? (EaseOfImplementation.Value * 10m + BudgetEstimation.Value * 15m + ProjectDependency.Value * 5m) / 30m
        : null;

    /// <summary>Calculated Business Value score (weighted average)</summary>
    public decimal? BusinessValueScore =>
        (StrategicAlignmentScore.HasValue && LeadershipDirections.HasValue && QualityOfLife.HasValue &&
         InnovationAndFutureShaping.HasValue && FinancialAndEconomicImpact.HasValue && SustainabilityScore.HasValue)
        ? (StrategicAlignmentScore.Value * 15m + LeadershipDirections.Value * 10m + QualityOfLife.Value * 15m +
           InnovationAndFutureShaping.Value * 5m + FinancialAndEconomicImpact.Value * 10m + SustainabilityScore.Value * 15m) / 70m
        : null;

    /// <summary>Total Prioritization Score (Business Readiness 30% + Business Value 70%)</summary>
    public decimal? TotalPrioritizationScore =>
        (BusinessReadinessScore.HasValue && BusinessValueScore.HasValue)
        ? (BusinessReadinessScore.Value * 0.30m + BusinessValueScore.Value * 0.70m)
        : null;

    // Navigation properties
    [Obsolete("Audit #2: use ImprovementProcesses (M2M).")]
    public Process? Process { get; set; }
    [Obsolete("Audit #2: use ImprovementServices (M2M).")]
    public Services.Service? Service { get; set; }
    public Services.StrategicObjective? StrategicObjective { get; set; }
    public ICollection<ImprovementProcess> ImprovementProcesses { get; set; } = new List<ImprovementProcess>();
    public ICollection<ImprovementService> ImprovementServices { get; set; } = new List<ImprovementService>();
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public User? Owner { get; set; }
    public OrganizationUnit? OwningUnit { get; set; }
    public ICollection<ImprovementAction> Actions { get; set; } = new List<ImprovementAction>();
    public ICollection<ImprovementMeasurement> Measurements { get; set; } = new List<ImprovementMeasurement>();
    public ICollection<ImprovementTeamMember> TeamMembers { get; set; } = new List<ImprovementTeamMember>();
    public ICollection<ImprovementRisk> ImprovementRisks { get; set; } = new List<ImprovementRisk>();
    /// <summary>Audit #7: assets this initiative replaces / upgrades / adds.</summary>
    public ICollection<ImprovementAsset> ImprovementAssets { get; set; } = new List<ImprovementAsset>();
    /// <summary>Audit #1: post-closure 3M / 6M / 12M benefits-realization checkpoints.</summary>
    public ICollection<ImprovementBenefitsReview> BenefitsReviews { get; set; } = new List<ImprovementBenefitsReview>();
    /// <summary>Audit #9: per-field immutable change log (populated by ImprovementChangeLogInterceptor).</summary>
    public ICollection<ImprovementChangeLog> ChangeLogs { get; set; } = new List<ImprovementChangeLog>();

    /// <summary>
    /// Calculates and sets the quadrant based on impact and effort scores.
    /// Cutoffs are tunable via <see cref="PrioritizationConfig"/> (audit #17);
    /// when the caller does not pass values we fall back to the historical
    /// 6 / 6 cut-offs so existing rows still render the same way.
    /// </summary>
    public void CalculateQuadrant(int impactCutoff = 6, int effortCutoff = 6)
    {
        bool highImpact = ImpactScore >= impactCutoff;
        bool highEffort = EffortScore >= effortCutoff;

        Quadrant = (highImpact, highEffort) switch
        {
            (true, false) => ImprovementQuadrant.QuickWins,      // High Impact, Low Effort
            (true, true) => ImprovementQuadrant.MajorProjects,   // High Impact, High Effort
            (false, false) => ImprovementQuadrant.FillIns,       // Low Impact, Low Effort
            (false, true) => ImprovementQuadrant.ThanklessTasks  // Low Impact, High Effort
        };
    }
}

