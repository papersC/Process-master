namespace ESEMS.Web.Security;

/// <summary>
/// Centralized authorization policy names.
///
/// <para>
/// The legacy five — <see cref="CanView"/>, <see cref="CanEdit"/>,
/// <see cref="CanDelete"/>, <see cref="CanApprove"/>, <see cref="CanAdmin"/> —
/// remain in place and load-bearing while Plan X rolls out. They are mapped
/// to <c>Permission</c>-claim-based policies in <c>Program.cs</c> so the
/// existing 156 <c>[Authorize(Policy = ...)]</c> attributes keep working.
/// </para>
/// <para>
/// New granular policies live on <see cref="Module"/> — e.g.
/// <c>AppPolicies.Module.Improvement.Edit</c>. Individual controllers can
/// migrate from a broad legacy policy to a specific module/action pair at
/// their own pace.
/// </para>
/// </summary>
public static class AppPolicies
{
    // ---- Legacy policies (still used by existing [Authorize] attributes) ----
    public const string CanView = "ESEMS.CanView";
    public const string CanEdit = "ESEMS.CanEdit";
    public const string CanDelete = "ESEMS.CanDelete";
    public const string CanApprove = "ESEMS.CanApprove";
    public const string CanAdmin = "ESEMS.CanAdmin";

    /// <summary>
    /// Granular <c>Module.Action</c> policy names. Every member here matches
    /// a permission key emitted at login from a user's <c>UserRoleGroup</c>
    /// assignments (see <c>AccountController.BuildSignInPrincipalAsync</c>)
    /// and one of the system-seeded role groups in <c>Program.cs</c>.
    /// </summary>
    public static class Module
    {
        public static class Improvement
        {
            public const string View    = "Improvement.View";
            public const string Create  = "Improvement.Create";
            public const string Edit    = "Improvement.Edit";
            public const string Delete  = "Improvement.Delete";
            public const string Approve = "Improvement.Approve";
            public const string Export  = "Improvement.Export";
        }
        public static class Measurement
        {
            public const string View   = "Measurement.View";
            public const string Create = "Measurement.Create";
            public const string Edit   = "Measurement.Edit";
            public const string Delete = "Measurement.Delete";
            public const string Export = "Measurement.Export";
        }
        public static class Process
        {
            public const string View   = "Process.View";
            public const string Create = "Process.Create";
            public const string Edit   = "Process.Edit";
            public const string Delete = "Process.Delete";
            public const string Export = "Process.Export";
        }
        public static class Service
        {
            public const string View   = "Service.View";
            public const string Create = "Service.Create";
            public const string Edit   = "Service.Edit";
            public const string Delete = "Service.Delete";
            public const string Export = "Service.Export";
        }
        public static class Risk
        {
            public const string View    = "Risk.View";
            public const string Create  = "Risk.Create";
            public const string Edit    = "Risk.Edit";
            public const string Delete  = "Risk.Delete";
            public const string Approve = "Risk.Approve";
            public const string Export  = "Risk.Export";
        }
        public static class Asset
        {
            public const string View   = "Asset.View";
            public const string Create = "Asset.Create";
            public const string Edit   = "Asset.Edit";
            public const string Delete = "Asset.Delete";
            public const string Export = "Asset.Export";
        }
        public static class Incident
        {
            public const string View   = "Incident.View";
            public const string Create = "Incident.Create";
            public const string Edit   = "Incident.Edit";
            public const string Delete = "Incident.Delete";
        }
        public static class Problem
        {
            public const string View   = "Problem.View";
            public const string Create = "Problem.Create";
            public const string Edit   = "Problem.Edit";
            public const string Delete = "Problem.Delete";
        }
        public static class Workflow
        {
            public const string View    = "Workflow.View";
            public const string Approve = "Workflow.Approve";
        }
        public static class Reports
        {
            public const string View   = "Reports.View";
            public const string Export = "Reports.Export";
        }
        public static class Users
        {
            public const string View   = "Users.View";
            public const string Create = "Users.Create";
            public const string Edit   = "Users.Edit";
            public const string Delete = "Users.Delete";
        }
        public static class Settings
        {
            public const string View = "Settings.View";
            public const string Edit = "Settings.Edit";
        }
        public static class Ai
        {
            public const string View = "Ai.View";
        }
        public static class Workload
        {
            public const string View   = "Workload.View";
            public const string Create = "Workload.Create";
            public const string Edit   = "Workload.Edit";
            public const string Delete = "Workload.Delete";
            public const string Export = "Workload.Export";
        }
        public static class ChangeRequest
        {
            public const string View    = "ChangeRequest.View";
            public const string Create  = "ChangeRequest.Create";
            public const string Edit    = "ChangeRequest.Edit";
            public const string Delete  = "ChangeRequest.Delete";
            public const string Approve = "ChangeRequest.Approve";
        }
        // Named WorkflowTask to avoid ambiguity with System.Threading.Tasks.Task
        // at call sites. Covers the user-facing task/to-do module.
        public static class WorkflowTask
        {
            public const string View   = "WorkflowTask.View";
            public const string Create = "WorkflowTask.Create";
            public const string Edit   = "WorkflowTask.Edit";
            public const string Delete = "WorkflowTask.Delete";
        }
        public static class OrganizationUnit
        {
            public const string View   = "OrganizationUnit.View";
            public const string Create = "OrganizationUnit.Create";
            public const string Edit   = "OrganizationUnit.Edit";
            public const string Delete = "OrganizationUnit.Delete";
        }
    }

    /// <summary>
    /// The full catalog of <c>Module.Action</c> permission strings. Used by
    /// <c>Program.cs</c> to register one authorization policy per entry.
    /// </summary>
    public static readonly string[] AllModuleActions =
    [
        Module.Improvement.View, Module.Improvement.Create, Module.Improvement.Edit,
        Module.Improvement.Delete, Module.Improvement.Approve, Module.Improvement.Export,
        Module.Measurement.View, Module.Measurement.Create, Module.Measurement.Edit,
        Module.Measurement.Delete, Module.Measurement.Export,
        Module.Process.View, Module.Process.Create, Module.Process.Edit,
        Module.Process.Delete, Module.Process.Export,
        Module.Service.View, Module.Service.Create, Module.Service.Edit,
        Module.Service.Delete, Module.Service.Export,
        Module.Risk.View, Module.Risk.Create, Module.Risk.Edit,
        Module.Risk.Delete, Module.Risk.Approve, Module.Risk.Export,
        Module.Asset.View, Module.Asset.Create, Module.Asset.Edit,
        Module.Asset.Delete, Module.Asset.Export,
        Module.Incident.View, Module.Incident.Create, Module.Incident.Edit, Module.Incident.Delete,
        Module.Problem.View, Module.Problem.Create, Module.Problem.Edit, Module.Problem.Delete,
        Module.Workflow.View, Module.Workflow.Approve,
        Module.Reports.View, Module.Reports.Export,
        Module.Users.View, Module.Users.Create, Module.Users.Edit, Module.Users.Delete,
        Module.Settings.View, Module.Settings.Edit,
        Module.Ai.View,
        Module.Workload.View, Module.Workload.Create, Module.Workload.Edit,
        Module.Workload.Delete, Module.Workload.Export,
        Module.ChangeRequest.View, Module.ChangeRequest.Create, Module.ChangeRequest.Edit,
        Module.ChangeRequest.Delete, Module.ChangeRequest.Approve,
        Module.WorkflowTask.View, Module.WorkflowTask.Create, Module.WorkflowTask.Edit,
        Module.WorkflowTask.Delete,
        Module.OrganizationUnit.View, Module.OrganizationUnit.Create, Module.OrganizationUnit.Edit,
        Module.OrganizationUnit.Delete
    ];
}
