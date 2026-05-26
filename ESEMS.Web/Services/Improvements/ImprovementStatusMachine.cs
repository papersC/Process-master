using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Services.Improvements;

/// <summary>
/// Finite-state machine for <see cref="ImprovementStatus"/>. Centralises the
/// legal transitions between statuses so every call site in the controller
/// and in the workflow service agrees on which states can follow which.
///
/// Type-safe (audit #4): all transitions, terminal-set membership and
/// UI action descriptors operate on the enum, not magic strings.
/// String overloads remain for callers that have not been migrated yet
/// (notably the workflow service and a handful of views) — they parse
/// the string into the enum and delegate.
/// </summary>
public static class ImprovementStatusMachine
{
    /// <summary>
    /// Statuses that block any further transition.
    /// </summary>
    public static readonly HashSet<ImprovementStatus> Terminal = new()
    {
        // Audit #1: Closed is no longer terminal — benefits realisation
        // continues for 3M/6M/12M afterwards. Sustained is the new
        // terminal state on the success path.
        ImprovementStatus.Sustained,
        ImprovementStatus.Cancelled
    };

    /// <summary>
    /// Approval-pending statuses (used by reviewer queues).
    /// </summary>
    public static readonly ImprovementStatus[] AwaitingApproval =
        { ImprovementStatus.UnderReview };

    /// <summary>
    /// Allowed transitions per current status. <see cref="ImprovementStatus.Cancelled"/>
    /// is reachable from any non-terminal state and is therefore added
    /// dynamically by <see cref="AllowedNext(ImprovementStatus)"/>.
    /// </summary>
    private static readonly Dictionary<ImprovementStatus, HashSet<ImprovementStatus>> Map = new()
    {
        [ImprovementStatus.Proposed]    = new() { ImprovementStatus.UnderReview },
        [ImprovementStatus.UnderReview] = new() { ImprovementStatus.Approved, ImprovementStatus.Rejected, ImprovementStatus.Proposed },
        [ImprovementStatus.Approved]    = new() { ImprovementStatus.InProgress },
        [ImprovementStatus.InProgress]  = new() { ImprovementStatus.OnHold, ImprovementStatus.Completed },
        [ImprovementStatus.OnHold]      = new() { ImprovementStatus.InProgress },
        [ImprovementStatus.Completed]   = new() { ImprovementStatus.Closed },
        // Audit #1: Closed → BenefitsRealization (3M review starts) → Sustained.
        [ImprovementStatus.Closed]              = new() { ImprovementStatus.BenefitsRealization },
        [ImprovementStatus.BenefitsRealization] = new() { ImprovementStatus.Sustained, ImprovementStatus.Closed /* roll back if review missed */ },
        [ImprovementStatus.Rejected]    = new() { ImprovementStatus.Proposed },
    };

    public static IReadOnlyCollection<ImprovementStatus> AllowedNext(ImprovementStatus current)
    {
        if (Terminal.Contains(current)) return Array.Empty<ImprovementStatus>();
        var set = Map.TryGetValue(current, out var allowed)
            ? new HashSet<ImprovementStatus>(allowed)
            : new HashSet<ImprovementStatus>();
        set.Add(ImprovementStatus.Cancelled);
        return set;
    }

    public static bool CanTransition(ImprovementStatus current, ImprovementStatus target)
        => AllowedNext(current).Contains(target);

    public static void EnsureTransition(ImprovementStatus current, ImprovementStatus target)
    {
        if (!CanTransition(current, target))
            throw new InvalidOperationException(
                $"Cannot transition improvement from '{current}' to '{target}'.");
    }

    // ------------------------------------------------------------------
    // String-based overloads — kept for callers (workflow service, some
    // controller arguments coming from HTTP form fields) that still use
    // the historical string representation. These parse and delegate.
    // ------------------------------------------------------------------

    public static IReadOnlyCollection<string> AllowedNext(string? current)
    {
        var parsed = TryParse(current);
        return parsed is null
            ? Array.Empty<string>()
            : AllowedNext(parsed.Value).Select(s => s.ToString()).ToArray();
    }

    public static bool CanTransition(string? current, string target)
    {
        var c = TryParse(current);
        var t = TryParse(target);
        return c.HasValue && t.HasValue && CanTransition(c.Value, t.Value);
    }

    public static void EnsureTransition(string? current, string target)
    {
        if (!CanTransition(current, target))
            throw new InvalidOperationException(
                $"Cannot transition improvement from '{current ?? "(null)"}' to '{target}'.");
    }

    private static ImprovementStatus? TryParse(string? value)
        => Enum.TryParse<ImprovementStatus>(value, ignoreCase: true, out var v) ? v : null;

    // ------------------------------------------------------------------
    // String constants — retained so legacy view code that compares to
    // the string literal continues to compile. New code should use the
    // enum directly.
    // ------------------------------------------------------------------
    public const string Proposed    = nameof(ImprovementStatus.Proposed);
    public const string UnderReview = nameof(ImprovementStatus.UnderReview);
    public const string Approved    = nameof(ImprovementStatus.Approved);
    public const string InProgress  = nameof(ImprovementStatus.InProgress);
    public const string OnHold      = nameof(ImprovementStatus.OnHold);
    public const string Completed   = nameof(ImprovementStatus.Completed);
    public const string Closed              = nameof(ImprovementStatus.Closed);
    public const string BenefitsRealization = nameof(ImprovementStatus.BenefitsRealization);
    public const string Sustained           = nameof(ImprovementStatus.Sustained);
    public const string Rejected            = nameof(ImprovementStatus.Rejected);
    public const string Cancelled           = nameof(ImprovementStatus.Cancelled);

    /// <summary>
    /// User-friendly action set for UI buttons. Maps a current status to the
    /// list of workflow actions the current user is allowed to take.
    /// </summary>
    public sealed record ActionDescriptor(string Key, ImprovementStatus TargetStatus, string LabelEn, string LabelAr, string Color, string Icon)
    {
        /// <summary>String form of the target status, for views that bind to string-typed form fields.</summary>
        public string TargetStatusString => TargetStatus.ToString();
    }

    public static IReadOnlyList<ActionDescriptor> ActionsFor(ImprovementStatus current)
    {
        // Design X: all action buttons use brand blue (#005B99) as primary
        // and slate (#64748b) as secondary.
        const string Primary   = "#005B99";
        const string Secondary = "#64748b";

        return current switch
        {
            ImprovementStatus.Proposed => new ActionDescriptor[]
            {
                new("Submit", ImprovementStatus.UnderReview, "Submit for Approval", "تقديم للاعتماد", Primary,   "send"),
                new("Cancel", ImprovementStatus.Cancelled,   "Cancel",              "إلغاء",         Secondary, "x"),
            },
            ImprovementStatus.UnderReview => new ActionDescriptor[]
            {
                new("Approve", ImprovementStatus.Approved, "Approve",            "اعتماد",        Primary,   "check"),
                new("Reject",  ImprovementStatus.Rejected, "Reject",             "رفض",          Secondary, "x-circle"),
                new("Return",  ImprovementStatus.Proposed, "Return for Changes", "إرجاع للتعديل", Secondary, "rotate-ccw"),
            },
            ImprovementStatus.Approved => new ActionDescriptor[]
            {
                new("Start",  ImprovementStatus.InProgress, "Start Execution", "بدء التنفيذ", Primary,   "play"),
                new("Cancel", ImprovementStatus.Cancelled,  "Cancel",          "إلغاء",       Secondary, "x"),
            },
            ImprovementStatus.InProgress => new ActionDescriptor[]
            {
                new("Pause", ImprovementStatus.OnHold,    "Put On Hold",      "تعليق",            Secondary, "pause"),
                new("Close", ImprovementStatus.Completed, "Close Initiative", "إغلاق المبادرة",   Primary,   "flag"),
            },
            ImprovementStatus.OnHold => new ActionDescriptor[]
            {
                new("Resume", ImprovementStatus.InProgress, "Resume", "استئناف", Primary,   "play"),
                new("Cancel", ImprovementStatus.Cancelled,  "Cancel", "إلغاء",   Secondary, "x"),
            },
            ImprovementStatus.Completed => new ActionDescriptor[]
            {
                new("Archive", ImprovementStatus.Closed, "Close & Begin Realization", "إغلاق وبدء قياس الأثر", Primary, "archive"),
            },
            // Audit #1: post-closure benefits-realization stage.
            ImprovementStatus.Closed => new ActionDescriptor[]
            {
                new("StartRealization", ImprovementStatus.BenefitsRealization, "Begin Benefits Tracking", "بدء قياس الأثر", Primary, "trending-up"),
            },
            ImprovementStatus.BenefitsRealization => new ActionDescriptor[]
            {
                new("Sustain",  ImprovementStatus.Sustained, "Mark Sustained (12M passed)", "تأكيد الأثر المستدام (مر 12 شهراً)", Primary,   "check-circle"),
                new("RollBack", ImprovementStatus.Closed,    "Pause Tracking",              "إيقاف القياس",                       Secondary, "rotate-ccw"),
            },
            ImprovementStatus.Rejected => new ActionDescriptor[]
            {
                new("Resubmit", ImprovementStatus.Proposed, "Edit & Resubmit", "تعديل وإعادة تقديم", Primary, "edit"),
            },
            _ => Array.Empty<ActionDescriptor>(),
        };
    }

    public static IReadOnlyList<ActionDescriptor> ActionsFor(string? current)
    {
        var parsed = TryParse(current) ?? ImprovementStatus.Proposed;
        return ActionsFor(parsed);
    }
}
