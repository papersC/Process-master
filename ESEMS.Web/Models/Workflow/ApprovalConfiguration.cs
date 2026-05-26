namespace ESEMS.Web.Models.Workflow;

/// <summary>
/// Approval rule for an entity type. Multiple rows per EntityType allowed —
/// WorkflowService evaluates them in Priority order (ascending) and picks
/// the first rule whose condition bands match the submission context.
/// A rule with no conditions is an unconditional fallback.
/// </summary>
public class ApprovalConfiguration
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string EntityType { get; set; } = string.Empty; // Improvement, ChangeRequest

    /// <summary>
    /// Friendly name shown in the rules table (e.g. "Quick Win", "Strategic").
    /// </summary>
    public string? RuleName { get; set; }

    /// <summary>
    /// Evaluation order — lower numbers are tried first. Allows the admin
    /// to put "Transformative" at priority 10 and "Quick Win" at priority 100
    /// so the most specific rule wins even if a broader one would also match.
    /// </summary>
    public int Priority { get; set; } = 100;

    // ── Condition bands ──────────────────────────────────────────────
    // Any null side of a band means "no constraint on this side". A rule
    // with no bands at all acts as a catch-all fallback.

    /// <summary>Minimum estimated cost savings (inclusive, AED).</summary>
    public decimal? MinCostSavings { get; set; }
    /// <summary>Maximum estimated cost savings (exclusive, AED).</summary>
    public decimal? MaxCostSavings { get; set; }

    /// <summary>Minimum impact score (1-10).</summary>
    public int? MinImpactScore { get; set; }
    /// <summary>Maximum impact score (1-10).</summary>
    public int? MaxImpactScore { get; set; }

    /// <summary>Minimum duration days (CreatedAt → TargetDate).</summary>
    public int? MinDurationDays { get; set; }
    /// <summary>Maximum duration days (CreatedAt → TargetDate).</summary>
    public int? MaxDurationDays { get; set; }

    /// <summary>Exact horizon match (Horizon1_Current / Horizon2_Expand / Horizon3_Future).</summary>
    public string? Horizon { get; set; }

    /// <summary>Exact innovation type match (Incremental / Breakthrough / Disruptive / Transformative).</summary>
    public string? InnovationType { get; set; }

    // ── Approver levels (unchanged semantics) ────────────────────────
    public bool Level1Required { get; set; } = true;
    public string Level1ApproverType { get; set; } = "SpecificUser"; // DirectManager, SpecificUser
    public int? Level1ApproverUserId { get; set; }
    public string? Level1ApproverName { get; set; }
    public bool Level2Required { get; set; }
    public string? Level2ApproverType { get; set; }
    public int? Level2ApproverUserId { get; set; }
    public string? Level2ApproverName { get; set; }

    // ── SLA + auto-escalation ─────────────────────────────────────────
    /// <summary>Hours before a Level-1 step escalates. Null = no SLA.</summary>
    public int? Level1SlaHours { get; set; }
    /// <summary>Hours before a Level-2 step escalates. Null = no SLA.</summary>
    public int? Level2SlaHours { get; set; }
    /// <summary>User who receives escalated approvals when the SLA lapses. Null = no escalation (SLA is informational only).</summary>
    public int? EscalationUserId { get; set; }
    public string? EscalationUserName { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Evaluates whether this rule applies to a given submission context.
    /// Empty/null sides skip that side of the band — a rule with all nulls
    /// matches every submission (catch-all fallback).
    /// </summary>
    public bool Matches(ApprovalContext ctx)
    {
        if (MinCostSavings.HasValue && (ctx.CostSavings ?? 0m) < MinCostSavings.Value) return false;
        if (MaxCostSavings.HasValue && (ctx.CostSavings ?? 0m) >= MaxCostSavings.Value) return false;
        if (MinImpactScore.HasValue && (ctx.ImpactScore ?? 0) < MinImpactScore.Value) return false;
        if (MaxImpactScore.HasValue && (ctx.ImpactScore ?? 0) > MaxImpactScore.Value) return false;
        if (MinDurationDays.HasValue && (ctx.DurationDays ?? 0) < MinDurationDays.Value) return false;
        if (MaxDurationDays.HasValue && (ctx.DurationDays ?? 0) > MaxDurationDays.Value) return false;
        if (!string.IsNullOrWhiteSpace(Horizon) && !string.Equals(Horizon, ctx.Horizon, StringComparison.OrdinalIgnoreCase)) return false;
        if (!string.IsNullOrWhiteSpace(InnovationType) && !string.Equals(InnovationType, ctx.InnovationType, StringComparison.OrdinalIgnoreCase)) return false;
        return true;
    }
}

/// <summary>
/// Lightweight context object passed to <see cref="ApprovalConfiguration.Matches"/>.
/// Controllers build this when they invoke <c>WorkflowService.CreateAsync</c>
/// so the service can pick the matching approval rule without coupling to
/// any specific entity type.
/// </summary>
public class ApprovalContext
{
    public decimal? CostSavings { get; set; }
    public int? ImpactScore { get; set; }
    public int? DurationDays { get; set; }
    public string? Horizon { get; set; }
    public string? InnovationType { get; set; }
}
