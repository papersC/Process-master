using ESEMS.Web.Models.Enums;

namespace ESEMS.Web.Services.Workload;

/// <summary>
/// Finite-state machine for <see cref="WorkloadScenarioStatus"/>. Centralises
/// the legal transitions so the Edit endpoint can't move a scenario into an
/// arbitrary state via form-post. Mirrors the pattern used by
/// <c>ImprovementStatusMachine</c>.
///
/// Flow
/// ----
///   Draft      → InReview, Archived
///   InReview   → Approved, Draft (return for edits), Archived
///   Approved   → InReview (revision), Archived
///   Archived   → (terminal)
/// </summary>
public static class WorkloadScenarioStatusMachine
{
    public static readonly HashSet<WorkloadScenarioStatus> Terminal = new()
    {
        WorkloadScenarioStatus.Archived
    };

    private static readonly Dictionary<WorkloadScenarioStatus, HashSet<WorkloadScenarioStatus>> Map = new()
    {
        [WorkloadScenarioStatus.Draft] = new()
        {
            WorkloadScenarioStatus.InReview,
            WorkloadScenarioStatus.Archived
        },
        [WorkloadScenarioStatus.InReview] = new()
        {
            WorkloadScenarioStatus.Approved,
            WorkloadScenarioStatus.Draft,
            WorkloadScenarioStatus.Archived
        },
        [WorkloadScenarioStatus.Approved] = new()
        {
            WorkloadScenarioStatus.InReview,
            WorkloadScenarioStatus.Archived
        },
    };

    /// <summary>
    /// True when the scenario may remain on <paramref name="current"/> (no-op)
    /// or transition to <paramref name="target"/>.
    /// </summary>
    public static bool CanTransition(WorkloadScenarioStatus current, WorkloadScenarioStatus target)
    {
        if (current == target) return true;
        return Map.TryGetValue(current, out var allowed) && allowed.Contains(target);
    }

    public static IReadOnlyCollection<WorkloadScenarioStatus> AllowedNext(WorkloadScenarioStatus current)
    {
        if (Terminal.Contains(current)) return Array.Empty<WorkloadScenarioStatus>();
        return Map.TryGetValue(current, out var allowed)
            ? allowed
            : (IReadOnlyCollection<WorkloadScenarioStatus>)Array.Empty<WorkloadScenarioStatus>();
    }

    /// <summary>
    /// Scenarios in a terminal state must not be mutated by the Edit form.
    /// </summary>
    public static bool IsEditable(WorkloadScenarioStatus current) => !Terminal.Contains(current);
}
