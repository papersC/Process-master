namespace ESEMS.Web.Models.Workflow;

public class WorkflowStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string WorkflowInstanceId { get; set; } = string.Empty;
    public int StepLevel { get; set; }
    public int? ApproverUserId { get; set; }
    public string? ApproverName { get; set; }
    public string Action { get; set; } = "Pending"; // Pending, Approved, Rejected, ReturnedForCorrection
    public string? Comments { get; set; }
    public DateTime? ActionDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── SLA + delegation ──────────────────────────────────────────────
    /// <summary>Escalation deadline — null = no SLA tracked on this step.</summary>
    public DateTime? DueAt { get; set; }
    /// <summary>When the step was auto-escalated (set by the SLA worker). Null = not yet.</summary>
    public DateTime? EscalatedAt { get; set; }
    /// <summary>Original approver when this step is currently delegated. Null = not delegated.</summary>
    public int? DelegatedFromUserId { get; set; }
    public string? DelegatedFromName { get; set; }
    /// <summary>Delegation return-date — when reached, the SLA worker reverts approver to DelegatedFromUserId.</summary>
    public DateTime? DelegationExpiresAt { get; set; }

    // Navigation
    public WorkflowInstance? WorkflowInstance { get; set; }
}
