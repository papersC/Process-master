using ESEMS.Web.Models.Workflow;

namespace ESEMS.Web.Services.Workflow;

public interface IWorkflowService
{
    Task<WorkflowInstance?> GetByIdAsync(string id);
    Task<List<WorkflowInstance>> GetPendingApprovalsAsync(int approverUserId);
    Task<WorkflowInstance> CreateAsync(string entityId, string entityType, int submitterId, string submitterName, string? notes = null, ApprovalContext? context = null);
    Task ProcessActionAsync(string workflowId, int approverUserId, string approverName, string action, string? comments = null);
    Task<List<WorkflowInstance>> GetByEntityAsync(string entityId, string entityType);
    /// <summary>
    /// Append a comment-only history entry to a workflow. Does not change the
    /// workflow's CurrentLevel or Status — the pending step remains pending.
    /// Used for the Pending Approvals 'Comment' affordance.
    /// </summary>
    Task AddCommentAsync(string workflowId, int commenterUserId, string commenterName, string comment);
}
