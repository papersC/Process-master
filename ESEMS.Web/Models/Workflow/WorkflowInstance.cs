namespace ESEMS.Web.Models.Workflow;

public enum WorkflowStatus
{
    Draft,
    Submitted,
    UnderReview,
    Approved,
    Rejected,
    ReturnedForCorrection,
    Cancelled
}

public class WorkflowInstance
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Type { get; set; } = string.Empty; // ChangeRequest, ImprovementInitiative, etc.
    public WorkflowStatus Status { get; set; } = WorkflowStatus.Draft;
    public string EntityId { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public int? SubmittedById { get; set; }
    public string? SubmitterName { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public int CurrentLevel { get; set; }
    public int MaxLevel { get; set; } = 1;
    public int? ApproverUserId { get; set; }
    /// <summary>
    /// ID of the <see cref="ApprovalConfiguration"/> rule that was selected
    /// at submission time. Lets ProcessActionAsync walk to the Level 2
    /// approver of the *same* rule even if multiple rules exist.
    /// </summary>
    public string? ApprovalConfigurationId { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public List<WorkflowStep> Steps { get; set; } = new();
}
