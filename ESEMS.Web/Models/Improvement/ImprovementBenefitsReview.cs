using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ESEMS.Web.Models.Improvement;

/// <summary>
/// Identifies which post-closure benefits-tracking checkpoint a
/// <see cref="ImprovementBenefitsReview"/> represents (audit #1).
/// </summary>
public enum BenefitsReviewPeriod
{
    /// <summary>3 months after Closure.</summary>
    ThreeMonths = 3,
    /// <summary>6 months after Closure.</summary>
    SixMonths = 6,
    /// <summary>12 months after Closure (final review before Sustained).</summary>
    TwelveMonths = 12
}

/// <summary>
/// Outcome a reviewer records at each post-closure benefits checkpoint.
/// Maps to the executive RAG view on the dashboard.
/// </summary>
public enum BenefitsReviewOutcome
{
    /// <summary>Pending — review is scheduled but not yet conducted.</summary>
    Pending,
    /// <summary>Benefits realised at or above target — green.</summary>
    Realized,
    /// <summary>Benefits partially realised — amber, needs intervention.</summary>
    PartiallyRealized,
    /// <summary>Benefits not realised — red, escalate to sponsor.</summary>
    NotRealized
}

/// <summary>
/// Post-closure benefits-realisation review (audit #1). Three rows per
/// closed initiative — at 3 / 6 / 12 months — capturing the actual benefits
/// observed against the targets set at closure.
///
/// Created up-front by <see cref="ESEMS.Web.Services.Improvements.BenefitsRealizationScheduler"/>
/// (background service) the moment an initiative transitions to Closed,
/// then filled in by a reviewer when each checkpoint comes due. Sponsor
/// sign-off on the 12-month review is what flips the initiative to
/// <c>Sustained</c>.
///
/// Why a separate entity rather than reusing <see cref="ImprovementReview"/>:
///   - Reviews are about execution health (Green/Amber/Red on progress),
///     this is about benefit verification (did the savings actually appear).
///   - Reviews can be ad-hoc; benefits reviews are scheduled by cadence.
///   - Sponsor sign-off semantics differ — a benefits review with sign-off
///     drives the state machine; a generic review is informational.
/// </summary>
[Table("ImprovementBenefitsReviews")]
public class ImprovementBenefitsReview
{
    [Key]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required, MaxLength(450)]
    public string ImprovementId { get; set; } = string.Empty;

    /// <summary>Which checkpoint this row represents (3/6/12 months).</summary>
    public BenefitsReviewPeriod Period { get; set; }

    /// <summary>
    /// When this checkpoint becomes "due" — auto-computed at scheduling time
    /// as <c>ClosedAt + Period months</c>. The background service ignores
    /// rows whose <see cref="DueDate"/> is in the future.
    /// </summary>
    public DateTime DueDate { get; set; }

    /// <summary>Reviewer's verdict.</summary>
    public BenefitsReviewOutcome Outcome { get; set; } = BenefitsReviewOutcome.Pending;

    /// <summary>Actual cost saving observed at this checkpoint (AED).</summary>
    public decimal? ActualCostSaving { get; set; }

    /// <summary>Actual time saving observed at this checkpoint (hours).</summary>
    public decimal? ActualTimeSaving { get; set; }

    /// <summary>Reviewer notes — what's working, what isn't, intervention needed.</summary>
    [MaxLength(2000)]
    public string? Notes { get; set; }

    /// <summary>Reviewer's user identity.</summary>
    [MaxLength(150)]
    public string? ReviewedById { get; set; }
    [MaxLength(200)]
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Sponsor sign-off — only the 12M review's sign-off drives the
    /// transition to Sustained. The earlier (3M, 6M) reviews can be filed
    /// by the owner alone.
    /// </summary>
    [MaxLength(150)]
    public string? SignedOffById { get; set; }
    [MaxLength(200)]
    public string? SignedOffByName { get; set; }
    public DateTime? SignedOffAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    [ForeignKey(nameof(ImprovementId))]
    public ImprovementInitiative? Improvement { get; set; }
}
