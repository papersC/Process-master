using Microsoft.EntityFrameworkCore;
using ESEMS.Web.Models;
using ESEMS.Web.Models.APQC;
using ESEMS.Web.Models.Services;
using ESEMS.Web.Models.Improvement;
using ESEMS.Web.Models.Common;
using ESEMS.Web.Models.ServiceManagement;
using ESEMS.Web.Models.AssetManagement;
using ESEMS.Web.Models.RiskManagement;
using ESEMS.Web.Models.Feedback;
using ESEMS.Web.Models.SLA;
using ESEMS.Web.Models.DocumentManagement;
using ESEMS.Web.Models.WorkloadAnalysis;
using ESEMS.Web.Models.Import;

namespace ESEMS.Web.Data;

/// <summary>
/// Application database context (without ASP.NET Identity tables)
/// </summary>
public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // APQC Hierarchy
    public DbSet<Category> Categories { get; set; }
    public DbSet<ProcessGroup> ProcessGroups { get; set; }
    public DbSet<Process> Processes { get; set; }
    public DbSet<Activity> Activities { get; set; }
    public DbSet<ProcessTask> ProcessTasks { get; set; }
    public DbSet<BpmnLane> BpmnLanes { get; set; } = null!;

    // Organization
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<OrganizationUnitResponsibility> OrganizationUnitResponsibilities { get; set; } = null!;
    public DbSet<ProcessResponsibility> ProcessResponsibilities { get; set; } = null!;
    public DbSet<ServiceResponsibility> ServiceResponsibilities { get; set; } = null!;
    public DbSet<JobPosition> JobPositions { get; set; } = null!;

    // Services
    public DbSet<Service> Services { get; set; }
    public DbSet<ServiceCategory> ServiceCategories { get; set; } = null!;
    public DbSet<ServiceCatalogInfo> ServiceCatalogInfos { get; set; } = null!;
    public DbSet<StrategicObjective> StrategicObjectives { get; set; }
    public DbSet<SystemDefinition> SystemDefinitions { get; set; }

    // Data import audit — one row per legacy import run, with an undo manifest
    public DbSet<ImportBatch> ImportBatches { get; set; } = null!;

    // Document Management — lookup tables for Process documentation metadata
    public DbSet<DocumentCategory> DocumentCategories { get; set; }
    public DbSet<DocumentType> DocumentTypes { get; set; }

    // Document Management — per-user "My Space" library and its links to processes
    public DbSet<UserDocument> UserDocuments { get; set; } = null!;
    public DbSet<ProcessDocument> ProcessDocuments { get; set; } = null!;

    // Measurements
    public DbSet<ServiceMeasurement> ServiceMeasurements { get; set; }
    public DbSet<ProcessMeasurement> ProcessMeasurements { get; set; }
    public DbSet<ProcessRisk> ProcessRisks { get; set; }

    // RACI
    public DbSet<ProcessRaci> ProcessRacis { get; set; }
    public DbSet<ActivityRaci> ActivityRacis { get; set; }
    public DbSet<TaskRaci> TaskRacis { get; set; }

    // Improvement
    public DbSet<ImprovementInitiative> ImprovementInitiatives { get; set; }
    public DbSet<ImprovementAction> ImprovementActions { get; set; }
    public DbSet<ImprovementMeasurement> ImprovementMeasurements { get; set; }
    public DbSet<ImprovementDraft> ImprovementDrafts { get; set; } = null!;
    public DbSet<MeasurementReading> MeasurementReadings { get; set; } = null!;
    public DbSet<ImprovementClosureReport> ImprovementClosureReports { get; set; } = null!;
    public DbSet<ImprovementReview> ImprovementReviews { get; set; } = null!;
    public DbSet<ImprovementTeamMember> ImprovementTeamMembers { get; set; }
    public DbSet<ImprovementProcess> ImprovementProcesses { get; set; }
    public DbSet<ImprovementService> ImprovementServices { get; set; }
    public DbSet<PrioritizationConfig> PrioritizationConfigs { get; set; } = null!;
    // Audit Batch B
    public DbSet<ImprovementAsset> ImprovementAssets { get; set; } = null!;             // #7
    public DbSet<ImprovementChangeLog> ImprovementChangeLogs { get; set; } = null!;     // #9
    public DbSet<ImprovementBenefitsReview> ImprovementBenefitsReviews { get; set; } = null!; // #1
    // Audit Batch C
    public DbSet<ReviewCycle> ReviewCycles { get; set; } = null!;                      // #16
    public DbSet<ImprovementReviewCycleAssignment> ImprovementReviewCycleAssignments { get; set; } = null!; // #16
    // Audit Batch D
    public DbSet<KpiDefinition> KpiDefinitions { get; set; } = null!;                  // #15
    public DbSet<ChangeRequest> ChangeRequests { get; set; }
    public DbSet<ChangeRequestComment> ChangeRequestComments { get; set; }

    // Audit
    public DbSet<AuditLog> AuditLogs { get; set; }

    // Service Management (ISO 20000-1:2018)
    public DbSet<Incident> Incidents { get; set; }
    public DbSet<Problem> Problems { get; set; }
    public DbSet<IncidentComment> IncidentComments { get; set; }
    public DbSet<ProblemComment> ProblemComments { get; set; }

    // Asset Management (ISO 55001:2014)
    public DbSet<Asset> Assets { get; set; }
    public DbSet<AssetCategory> AssetCategories { get; set; }
    public DbSet<MaintenanceSchedule> MaintenanceSchedules { get; set; }
    public DbSet<MaintenanceRecord> MaintenanceRecords { get; set; }
    public DbSet<AssetRisk> AssetRisks { get; set; }

    // Risk Management (ISO 31000:2018)
    public DbSet<EnterpriseRisk> EnterpriseRisks { get; set; }
    public DbSet<RiskCategory> RiskCategories { get; set; }
    public DbSet<RiskActionPlan> RiskActionPlans { get; set; }

    // Asset-Risk-Service Relationships
    public DbSet<ServiceAsset> ServiceAssets { get; set; }
    public DbSet<ServiceRisk> ServiceRisks { get; set; }
    public DbSet<ProcessService> ProcessServices { get; set; }
    public DbSet<ProcessStrategicObjective> ProcessStrategicObjectives { get; set; }
    public DbSet<ServiceStrategicObjective> ServiceStrategicObjectives { get; set; }
    public DbSet<ChangeRequestAsset> ChangeRequestAssets { get; set; }
    public DbSet<ChangeRequestRisk> ChangeRequestRisks { get; set; }
    public DbSet<ImprovementRisk> ImprovementRisks { get; set; }

    // Customer Feedback (ISO 9001:2015)
    public DbSet<CustomerFeedback> CustomerFeedbacks { get; set; }
    public DbSet<FeedbackCategory> FeedbackCategories { get; set; }

    // SLA Monitoring (ISO 20000-1:2018)
    public DbSet<SLADefinition> SLADefinitions { get; set; }
    public DbSet<SLABreach> SLABreaches { get; set; }

    // BPMN Version History
    public DbSet<ProcessBpmnVersion> ProcessBpmnVersions { get; set; }

    // Process Maturity Assessment (Draft7 - APQC Pillars)
    public DbSet<ProcessMaturityAssessment> ProcessMaturityAssessments { get; set; }

    // Service Assessment 360 (Draft8 - 7 Criteria)
    public DbSet<ServiceAssessment> ServiceAssessments { get; set; }

    // KPI Trends (Draft7 - Historical Performance)
    public DbSet<KPITrend> KPITrends { get; set; }

    // ISO Standards Compliance (Draft7 - 22+ Standards)
    public DbSet<ISOStandard> ISOStandards { get; set; }

    // Custom User Management (using existing database tables)
    public DbSet<CustomUser> CustomUsers { get; set; }

    // Notifications
    public DbSet<Models.Notifications.Notification> Notifications { get; set; } = null!;
    public DbSet<Models.Notifications.NotificationPreference> NotificationPreferences { get; set; } = null!;

    // Workflow
    public DbSet<Models.Workflow.WorkflowInstance> WorkflowInstances { get; set; } = null!;
    public DbSet<Models.Workflow.WorkflowStep> WorkflowSteps { get; set; } = null!;
    public DbSet<Models.Workflow.ApprovalConfiguration> ApprovalConfigurations { get; set; } = null!;

    // Generic application settings (Settings Hub — General/Email/Alerts/AI tabs)
    public DbSet<Models.Common.AppSetting> AppSettings { get; set; } = null!;

    // Role groups (Settings Hub — Role Groups page)
    public DbSet<Models.Common.RoleGroup> RoleGroups { get; set; } = null!;

    // Plan X: user ↔ role-group junction for matrix-driven authorization
    public DbSet<Models.Common.UserRoleGroup> UserRoleGroups { get; set; } = null!;

    // Workload Analysis (FTE / staffing calculations)
    public DbSet<WorkloadConfig> WorkloadConfigs { get; set; } = null!;
    public DbSet<WorkloadScenario> WorkloadScenarios { get; set; } = null!;
    public DbSet<WorkloadLineItem> WorkloadLineItems { get; set; } = null!;

    // Aliases for compatibility with Identity-based code
    public DbSet<CustomUser> Users => CustomUsers;

    protected override void OnModelCreating(ModelBuilder builder)
    {
        // Ignore the old Identity-based User model
        builder.Ignore<User>();

        // Configure APQC Hierarchy relationships
        ConfigureAPQCHierarchy(builder);

        // Configure Organization relationships
        ConfigureOrganization(builder);

        // Configure Service relationships
        ConfigureServices(builder);

        // Configure RACI relationships
        ConfigureRaci(builder);

        // Configure Improvement relationships
        ConfigureImprovement(builder);

        // Configure Audit
        ConfigureAudit(builder);

        // Configure Service Management
        ConfigureServiceManagement(builder);

        // Configure Asset Management
        ConfigureAssetManagement(builder);

        // Configure Risk Management
        ConfigureRiskManagement(builder);

        // Configure Customer Feedback
        ConfigureCustomerFeedback(builder);

        // Configure SLA Monitoring
        ConfigureSLAMonitoring(builder);

        // Configure BPMN Version History
        ConfigureBpmnVersionHistory(builder);

        // Configure Custom User Management
        ConfigureCustomUserManagement(builder);

        // Configure My Space / per-user documents and process document links
        ConfigureDocumentLinking(builder);

        // Configure indexes
        ConfigureIndexes(builder);

        // Configure Workload Analysis
        ConfigureWorkloadAnalysis(builder);

        // Configure decimal precision — see ConfigureDecimalPrecisions for the
        // matrix. Without these the EF model defaults to decimal(18,2) and
        // silently truncates KPI / FTE / currency values.
        ConfigureDecimalPrecisions(builder);

        // Workflow relationships
        builder.Entity<Models.Workflow.WorkflowStep>()
            .HasOne(s => s.WorkflowInstance)
            .WithMany(w => w.Steps)
            .HasForeignKey(s => s.WorkflowInstanceId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureAPQCHierarchy(ModelBuilder builder)
    {
        // Category -> ProcessGroups
        builder.Entity<Category>()
            .HasMany(c => c.ProcessGroups)
            .WithOne(pg => pg.Category)
            .HasForeignKey(pg => pg.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // ProcessGroup -> Processes
        builder.Entity<ProcessGroup>()
            .HasMany(pg => pg.Processes)
            .WithOne(p => p.ProcessGroup)
            .HasForeignKey(p => p.ProcessGroupId)
            .OnDelete(DeleteBehavior.Restrict);

        // Process -> Activities
        builder.Entity<Process>()
            .HasMany(p => p.Activities)
            .WithOne(a => a.Process)
            .HasForeignKey(a => a.ProcessId)
            .OnDelete(DeleteBehavior.Restrict);

        // Process -> DocumentCategory (nullable lookup FK)
        builder.Entity<Process>()
            .HasOne(p => p.DocumentCategory)
            .WithMany()
            .HasForeignKey(p => p.DocumentCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        // Process -> DocumentType (nullable lookup FK)
        builder.Entity<Process>()
            .HasOne(p => p.DocumentType)
            .WithMany()
            .HasForeignKey(p => p.DocumentTypeId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<DocumentCategory>().HasIndex(d => d.Code).IsUnique();
        builder.Entity<DocumentType>().HasIndex(d => d.Code).IsUnique();

        // Process: IsAutomated (inherited from MeasurableEntity) is superseded
        // by AutomationStatus enum. Tell EF to ignore the inherited property so
        // the Processes table column can be dropped without confusing the model.
        builder.Entity<Process>().Ignore(p => p.IsAutomated);

        // Activity -> Tasks
        builder.Entity<Activity>()
            .HasMany(a => a.Tasks)
            .WithOne(t => t.Activity)
            .HasForeignKey(t => t.ActivityId)
            .OnDelete(DeleteBehavior.Restrict);
    }

    private void ConfigureOrganization(ModelBuilder builder)
    {
        // Self-referencing hierarchy
        builder.Entity<OrganizationUnit>()
            .HasOne(o => o.Parent)
            .WithMany(o => o.Children)
            .HasForeignKey(o => o.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Organization -> Head User (User is [NotMapped], so no EF relationship)
        // HeadUserId is stored as a string FK but not enforced by EF

        // OrganizationUnitResponsibility -> OrganizationUnit
        // Cascade because responsibilities belong to the unit; deleting the unit
        // (hard delete) should clear them. Soft-delete on the unit leaves them.
        builder.Entity<OrganizationUnitResponsibility>()
            .HasOne(r => r.OrganizationUnit)
            .WithMany(o => o.OwnedResponsibilities)
            .HasForeignKey(r => r.OrganizationUnitId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureServices(ModelBuilder builder)
    {
        // Service -> ServiceCategory (flat lookup; SetNull on delete so the
        // service survives — falls back to the "uncategorized" state until
        // an admin reassigns it)
        builder.Entity<Service>()
            .HasOne(s => s.ServiceCategory)
            .WithMany(c => c.Services)
            .HasForeignKey(s => s.ServiceCategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ServiceCategory>().HasIndex(c => c.Code).IsUnique();
        builder.Entity<ServiceCategory>().HasIndex(c => c.IsDeleted);

        // Service -> StrategicObjective
        builder.Entity<Service>()
            .HasOne(s => s.StrategicObjective)
            .WithMany(so => so.Services)
            .HasForeignKey(s => s.StrategicObjectiveId)
            .OnDelete(DeleteBehavior.SetNull);

        // Process -> Service (DEPRECATED - kept for backward compatibility)
        builder.Entity<Process>()
            .HasOne(p => p.Service)
            .WithMany(s => s.Processes)
            .HasForeignKey(p => p.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // ProcessService many-to-many relationship
        builder.Entity<ProcessService>()
            .HasKey(ps => new { ps.ProcessId, ps.ServiceId });

        builder.Entity<ProcessService>()
            .HasOne(ps => ps.Process)
            .WithMany(p => p.ProcessServices)
            .HasForeignKey(ps => ps.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProcessService>()
            .HasOne(ps => ps.Service)
            .WithMany(s => s.ProcessServices)
            .HasForeignKey(ps => ps.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        // Process -> StrategicObjective (single FK - kept for backward compatibility)
        builder.Entity<Process>()
            .HasOne(p => p.StrategicObjective)
            .WithMany(so => so.Processes)
            .HasForeignKey(p => p.StrategicObjectiveId)
            .OnDelete(DeleteBehavior.SetNull);

        // ProcessResponsibility many-to-many — Process ↔ OrganizationUnitResponsibility
        builder.Entity<ProcessResponsibility>()
            .HasKey(pr => new { pr.ProcessId, pr.ResponsibilityId });

        builder.Entity<ProcessResponsibility>()
            .HasOne(pr => pr.Process)
            .WithMany(p => p.ProcessResponsibilities)
            .HasForeignKey(pr => pr.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProcessResponsibility>()
            .HasOne(pr => pr.Responsibility)
            .WithMany(r => r.LinkedProcesses)
            .HasForeignKey(pr => pr.ResponsibilityId)
            .OnDelete(DeleteBehavior.Cascade);

        // ServiceResponsibility many-to-many — Service ↔ OrganizationUnitResponsibility
        builder.Entity<ServiceResponsibility>()
            .HasKey(sr => new { sr.ServiceId, sr.ResponsibilityId });

        builder.Entity<ServiceResponsibility>()
            .HasOne(sr => sr.Service)
            .WithMany(s => s.ServiceResponsibilities)
            .HasForeignKey(sr => sr.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ServiceResponsibility>()
            .HasOne(sr => sr.Responsibility)
            .WithMany(r => r.LinkedServices)
            .HasForeignKey(sr => sr.ResponsibilityId)
            .OnDelete(DeleteBehavior.Cascade);

        // ProcessStrategicObjective many-to-many relationship
        builder.Entity<ProcessStrategicObjective>()
            .HasKey(pso => new { pso.ProcessId, pso.StrategicObjectiveId });

        builder.Entity<ProcessStrategicObjective>()
            .HasOne(pso => pso.Process)
            .WithMany(p => p.ProcessStrategicObjectives)
            .HasForeignKey(pso => pso.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProcessStrategicObjective>()
            .HasOne(pso => pso.StrategicObjective)
            .WithMany(so => so.ProcessStrategicObjectives)
            .HasForeignKey(pso => pso.StrategicObjectiveId)
            .OnDelete(DeleteBehavior.Cascade);

        // ServiceStrategicObjective many-to-many relationship
        builder.Entity<ServiceStrategicObjective>()
            .HasKey(sso => new { sso.ServiceId, sso.StrategicObjectiveId });

        builder.Entity<ServiceStrategicObjective>()
            .HasOne(sso => sso.Service)
            .WithMany(s => s.ServiceStrategicObjectives)
            .HasForeignKey(sso => sso.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ServiceStrategicObjective>()
            .HasOne(sso => sso.StrategicObjective)
            .WithMany(so => so.ServiceStrategicObjectives)
            .HasForeignKey(sso => sso.StrategicObjectiveId)
            .OnDelete(DeleteBehavior.Cascade);

        // Process -> System
        builder.Entity<Process>()
            .HasOne(p => p.System)
            .WithMany(s => s.Processes)
            .HasForeignKey(p => p.SystemId)
            .OnDelete(DeleteBehavior.SetNull);

        // ProcessTask -> System
        builder.Entity<ProcessTask>()
            .HasOne(t => t.System)
            .WithMany(s => s.Tasks)
            .HasForeignKey(t => t.SystemId)
            .OnDelete(DeleteBehavior.SetNull);

        // StrategicObjective self-referencing hierarchy
        builder.Entity<StrategicObjective>()
            .HasOne(so => so.Parent)
            .WithMany(so => so.Children)
            .HasForeignKey(so => so.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Measurements
        builder.Entity<ServiceMeasurement>()
            .HasOne(m => m.Service)
            .WithMany(s => s.Measurements)
            .HasForeignKey(m => m.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProcessMeasurement>()
            .HasOne(m => m.Process)
            .WithMany(p => p.Measurements)
            .HasForeignKey(m => m.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProcessRisk>()
            .HasOne(r => r.Process)
            .WithMany(p => p.Risks)
            .HasForeignKey(r => r.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        // ProcessRisk -> EnterpriseRisk (optional link to enterprise risk register)
        builder.Entity<ProcessRisk>()
            .HasOne(r => r.EnterpriseRisk)
            .WithMany()
            .HasForeignKey(r => r.EnterpriseRiskId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureRaci(ModelBuilder builder)
    {
        builder.Entity<ProcessRaci>()
            .HasOne(r => r.Process)
            .WithMany(p => p.RaciMatrix)
            .HasForeignKey(r => r.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ActivityRaci>()
            .HasOne(r => r.Activity)
            .WithMany(a => a.RaciMatrix)
            .HasForeignKey(r => r.ActivityId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<TaskRaci>()
            .HasOne(r => r.Task)
            .WithMany(t => t.RaciMatrix)
            .HasForeignKey(r => r.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Optional JobPosition FK on each RACI level — nullable, SetNull on delete
        // so removing a JobPosition from the catalog demotes RACI rows back to
        // unit-only coarse form rather than nuking them.
        builder.Entity<ProcessRaci>()
            .HasOne(r => r.JobPosition)
            .WithMany()
            .HasForeignKey(r => r.JobPositionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ActivityRaci>()
            .HasOne(r => r.JobPosition)
            .WithMany()
            .HasForeignKey(r => r.JobPositionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<TaskRaci>()
            .HasOne(r => r.JobPosition)
            .WithMany()
            .HasForeignKey(r => r.JobPositionId)
            .OnDelete(DeleteBehavior.SetNull);

        // JobPosition catalog itself — soft-delete-aware, code unique when present.
        builder.Entity<JobPosition>().HasIndex(j => j.IsDeleted);
        builder.Entity<JobPosition>().HasIndex(j => j.Code);
        builder.Entity<JobPosition>()
            .HasOne(j => j.OrganizationUnit)
            .WithMany()
            .HasForeignKey(j => j.OrganizationUnitId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureImprovement(ModelBuilder builder)
    {
        // Optimistic concurrency on Version. The controller increments
        // Version on every Edit; EF includes it in the WHERE clause so a
        // stale POST (Tab B saving after Tab A) throws
        // DbUpdateConcurrencyException instead of silently overwriting.
        // The Edit form already posts Version as a hidden input.
        builder.Entity<ImprovementInitiative>()
            .Property(i => i.Version)
            .IsConcurrencyToken();

        // Audit #2: legacy single-FK Process / Service kept as a deprecation
        // shim — too many call sites depend on them to drop in one batch.
        // The M2M tables (ImprovementProcess / ImprovementService) are
        // canonical; the migration backfills the M2M from these columns at
        // boot, so reports SHOULD query the M2M not the single FK.
#pragma warning disable CS0618 // Type or member is obsolete (the obsolete pragma is the point)
        builder.Entity<ImprovementInitiative>()
            .HasOne(i => i.Process)
            .WithMany()
            .HasForeignKey(i => i.ProcessId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ImprovementInitiative>()
            .HasOne(i => i.Service)
            .WithMany()
            .HasForeignKey(i => i.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);
#pragma warning restore CS0618

        // Audit #3: direct strategic anchor.
        builder.Entity<ImprovementInitiative>()
            .HasOne(i => i.StrategicObjective)
            .WithMany()
            .HasForeignKey(i => i.StrategicObjectiveId)
            .OnDelete(DeleteBehavior.SetNull);

        // Audit #7: ImprovementAsset M2M.
        builder.Entity<ImprovementAsset>(e =>
        {
            e.ToTable("ImprovementAssets");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Improvement)
                .WithMany(i => i.ImprovementAssets)
                .HasForeignKey(x => x.ImprovementId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Asset)
                .WithMany()
                .HasForeignKey(x => x.AssetId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.RelationshipType).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.ImprovementId);
            e.HasIndex(x => x.AssetId);
            e.HasIndex(x => new { x.ImprovementId, x.AssetId }).IsUnique();
        });

        // Audit #9: per-initiative immutable change log.
        builder.Entity<ImprovementChangeLog>(e =>
        {
            e.ToTable("ImprovementChangeLogs");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Improvement)
                .WithMany(i => i.ChangeLogs)
                .HasForeignKey(x => x.ImprovementId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ImprovementId);
            e.HasIndex(x => x.ChangedAt);
        });

        // Audit #1: post-closure benefits-realization checkpoints.
        builder.Entity<ImprovementBenefitsReview>(e =>
        {
            e.ToTable("ImprovementBenefitsReviews");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Improvement)
                .WithMany(i => i.BenefitsReviews)
                .HasForeignKey(x => x.ImprovementId)
                .OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.Period).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Outcome).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.ImprovementId);
            e.HasIndex(x => new { x.ImprovementId, x.Period }).IsUnique();
            e.HasIndex(x => x.DueDate);
        });

        // Audit #16: recurring stage-gate review cycle + M2M assignment.
        builder.Entity<ReviewCycle>(e =>
        {
            e.ToTable("ReviewCycles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Cadence).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.IsActive);
        });
        builder.Entity<ImprovementReviewCycleAssignment>(e =>
        {
            e.ToTable("ImprovementReviewCycleAssignments");
            e.HasKey(x => x.Id);
            e.HasOne(x => x.Improvement)
                .WithMany()
                .HasForeignKey(x => x.ImprovementId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.ReviewCycle)
                .WithMany(c => c.Assignments)
                .HasForeignKey(x => x.ReviewCycleId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => x.ImprovementId);
            e.HasIndex(x => x.ReviewCycleId);
            e.HasIndex(x => new { x.ImprovementId, x.ReviewCycleId }).IsUnique();
        });

        // Audit #4 / #11 / #19: persist the new enums as their nvarchar names
        // so existing rows ("Proposed", "InProgress", "HigherBetter", ...)
        // round-trip unchanged. No DB column type change needed.
        builder.Entity<ImprovementInitiative>()
            .Property(i => i.Status)
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Entity<ImprovementAction>()
            .Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(50);
        builder.Entity<ImprovementMeasurement>()
            .Property(m => m.Direction)
            .HasConversion<string>()
            .HasMaxLength(20);

        // ImprovementInitiative <-> Process (many-to-many via ImprovementProcess)
        builder.Entity<ImprovementProcess>()
            .HasKey(ip => new { ip.ImprovementId, ip.ProcessId });

        builder.Entity<ImprovementProcess>()
            .HasOne(ip => ip.Improvement)
            .WithMany(i => i.ImprovementProcesses)
            .HasForeignKey(ip => ip.ImprovementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ImprovementProcess>()
            .HasOne(ip => ip.Process)
            .WithMany()
            .HasForeignKey(ip => ip.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        // ImprovementInitiative <-> Service (many-to-many via ImprovementService)
        builder.Entity<ImprovementService>()
            .HasKey(isv => new { isv.ImprovementId, isv.ServiceId });

        builder.Entity<ImprovementService>()
            .HasOne(isv => isv.Improvement)
            .WithMany(i => i.ImprovementServices)
            .HasForeignKey(isv => isv.ImprovementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ImprovementService>()
            .HasOne(isv => isv.Service)
            .WithMany()
            .HasForeignKey(isv => isv.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ImprovementAction>()
            .HasOne(a => a.Improvement)
            .WithMany(i => i.Actions)
            .HasForeignKey(a => a.ImprovementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ImprovementMeasurement>()
            .HasOne(m => m.Improvement)
            .WithMany(i => i.Measurements)
            .HasForeignKey(m => m.ImprovementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ImprovementTeamMember>()
            .HasOne(tm => tm.Improvement)
            .WithMany(i => i.TeamMembers)
            .HasForeignKey(tm => tm.ImprovementId)
            .OnDelete(DeleteBehavior.Cascade);

        // Audit #14: TeamMember.UserId is now a real int FK to CustomUser.
        // Restrict-on-delete so an attempt to delete a user with active team
        // memberships fails loudly rather than orphaning the row.
        builder.Entity<ImprovementTeamMember>()
            .HasOne(tm => tm.User)
            .WithMany()
            .HasForeignKey(tm => tm.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Audit #13: ImprovementMeasurement scopes to a Process or Service via
        // real FKs (replacing the old "process:{id}" / "service:{id}" magic
        // string). Both nullable; SetNull on delete so removing a Process /
        // Service does not cascade and orphan the measurement.
        builder.Entity<ImprovementMeasurement>()
            .HasOne(m => m.AppliesToProcess)
            .WithMany()
            .HasForeignKey(m => m.AppliesToProcessId)
            .OnDelete(DeleteBehavior.SetNull);
        builder.Entity<ImprovementMeasurement>()
            .HasOne(m => m.AppliesToService)
            .WithMany()
            .HasForeignKey(m => m.AppliesToServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Audit #15: optional FK into the KPI catalog. SetNull on delete so
        // retiring a definition doesn't break historical measurements.
        builder.Entity<ImprovementMeasurement>()
            .HasOne(m => m.KpiDefinition)
            .WithMany()
            .HasForeignKey(m => m.KpiDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<KpiDefinition>(e =>
        {
            e.ToTable("KpiDefinitions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DefaultType).HasConversion<string>().HasMaxLength(30);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.IsActive);
            e.HasIndex(x => x.IsDeleted);
        });

        builder.Entity<ChangeRequest>()
            .HasOne(cr => cr.Process)
            .WithMany()
            .HasForeignKey(cr => cr.ProcessId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ChangeRequest>()
            .HasOne(cr => cr.Service)
            .WithMany()
            .HasForeignKey(cr => cr.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<ChangeRequestComment>()
            .HasOne(c => c.ChangeRequest)
            .WithMany(cr => cr.Comments)
            .HasForeignKey(c => c.ChangeRequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureAudit(ModelBuilder builder)
    {
        // AuditLog -> User (User is [NotMapped], so no EF relationship)
        // UserId is stored as a string FK but not enforced by EF
    }

    private void ConfigureIndexes(ModelBuilder builder)
    {
        // Soft-delete indexes on every entity that uses `IsDeleted` for
        // row-level visibility. Without these, every filtered list query
        // (`.Where(x => !x.IsDeleted)`) becomes a table scan once the table
        // crosses ~100k rows. Auditor flagged 30+ filter uses on unindexed
        // columns — this fixes the entire APQC hierarchy + service domain
        // in one pass. Composite indexes also cover the common sort order
        // (Code) so DataTables-driven lists stay index-backed.

        // Category indexes
        builder.Entity<Category>().HasIndex(c => c.Code).IsUnique();
        builder.Entity<Category>().HasIndex(c => c.IsDeleted);

        // ProcessGroup indexes
        builder.Entity<ProcessGroup>().HasIndex(pg => pg.Code).IsUnique();
        builder.Entity<ProcessGroup>().HasIndex(pg => pg.CategoryId);
        builder.Entity<ProcessGroup>().HasIndex(pg => pg.IsDeleted);

        // Process indexes
        builder.Entity<Process>().HasIndex(p => p.Code).IsUnique();
        builder.Entity<Process>().HasIndex(p => p.ProcessGroupId);
        builder.Entity<Process>().HasIndex(p => p.Status);
        builder.Entity<Process>().HasIndex(p => p.IsDeleted);

        // Activity indexes
        builder.Entity<Activity>().HasIndex(a => a.Code).IsUnique();
        builder.Entity<Activity>().HasIndex(a => a.ProcessId);
        builder.Entity<Activity>().HasIndex(a => a.IsDeleted);

        // ProcessTask indexes
        builder.Entity<ProcessTask>().HasIndex(t => t.Code).IsUnique();
        builder.Entity<ProcessTask>().HasIndex(t => t.ActivityId);
        builder.Entity<ProcessTask>().HasIndex(t => t.IsDeleted);

        // OrganizationUnit indexes
        builder.Entity<OrganizationUnit>().HasIndex(o => o.Code).IsUnique();
        builder.Entity<OrganizationUnit>().HasIndex(o => o.ParentId);
        builder.Entity<OrganizationUnit>().HasIndex(o => o.IsDeleted);

        // OrganizationUnitResponsibility indexes
        builder.Entity<OrganizationUnitResponsibility>().HasIndex(r => r.OrganizationUnitId);
        builder.Entity<OrganizationUnitResponsibility>().HasIndex(r => r.IsDeleted);

        // Service indexes
        builder.Entity<Service>().HasIndex(s => s.Code).IsUnique();
        builder.Entity<Service>().HasIndex(s => s.IsDeleted);

        // ServiceCatalogInfo — 1:1 with Service via unique FK. Cascade on
        // delete so a hard-deleted Service takes its catalog row with it
        // (soft-deletes leave the row intact, like all other Service kids).
        builder.Entity<ServiceCatalogInfo>()
            .HasOne(ci => ci.Service)
            .WithOne(s => s.CatalogInfo)
            .HasForeignKey<ServiceCatalogInfo>(ci => ci.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<ServiceCatalogInfo>()
            .HasIndex(ci => ci.ServiceId).IsUnique();
        builder.Entity<ServiceCatalogInfo>()
            .HasIndex(ci => ci.IsPublished);

        // ImportBatch — legacy-import undo manifest
        builder.Entity<ImportBatch>(e =>
        {
            e.Property(b => b.Manifest).HasColumnType("nvarchar(max)");
            e.Property(b => b.Kind).HasMaxLength(64);
            e.Property(b => b.FileName).HasMaxLength(260);
            e.Property(b => b.CreatedByName).HasMaxLength(256);
            e.HasIndex(b => new { b.IsReverted, b.CreatedAt });
        });

        // StrategicObjective indexes
        builder.Entity<StrategicObjective>().HasIndex(so => so.Code).IsUnique();

        // AuditLog indexes
        builder.Entity<AuditLog>().HasIndex(a => a.Timestamp);
        builder.Entity<AuditLog>().HasIndex(a => a.EntityType);
        builder.Entity<AuditLog>().HasIndex(a => a.EntityId);
        builder.Entity<AuditLog>().HasIndex(a => a.UserId);

        // Incident indexes
        builder.Entity<Incident>().HasIndex(i => i.IncidentNumber).IsUnique();
        builder.Entity<Incident>().HasIndex(i => i.Status);
        builder.Entity<Incident>().HasIndex(i => i.Priority);
        builder.Entity<Incident>().HasIndex(i => i.ReportedAt);
        builder.Entity<Incident>().HasIndex(i => i.IsDeleted);

        // Problem indexes
        builder.Entity<Problem>().HasIndex(p => p.ProblemNumber).IsUnique();
        builder.Entity<Problem>().HasIndex(p => p.Status);
        builder.Entity<Problem>().HasIndex(p => p.IsDeleted);

        // Asset indexes
        builder.Entity<Asset>().HasIndex(a => a.AssetTag).IsUnique();
        builder.Entity<Asset>().HasIndex(a => a.Status);
        builder.Entity<Asset>().HasIndex(a => a.IsDeleted);
        builder.Entity<Asset>().HasIndex(a => a.ConstructionStatus);
        builder.Entity<Asset>().HasIndex(a => a.ParentProjectId);
        builder.Entity<Asset>().HasIndex(a => a.Classification);
        builder.Entity<Asset>().HasIndex(a => a.DataOwnerUserId);
        // GPS columns need 6 decimal places (~11 cm precision at the
        // equator). The EF default of decimal(18,2) gives ~1.1 km which
        // is useless for plot-level mapping.
        builder.Entity<Asset>().Property(a => a.GpsLatitude).HasPrecision(9, 6);
        builder.Entity<Asset>().Property(a => a.GpsLongitude).HasPrecision(9, 6);
        // Self-FK so a villa can roll up to its parent housing project.
        // Restrict on delete — deleting a project shouldn't silently
        // orphan the units inside it; the user has to unhook or reassign
        // children first.
        builder.Entity<Asset>()
            .HasOne(a => a.ParentProject)
            .WithMany(a => a.ChildAssets)
            .HasForeignKey(a => a.ParentProjectId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<AssetCategory>().HasIndex(ac => ac.Code).IsUnique();
        builder.Entity<AssetCategory>().HasIndex(ac => ac.IsDeleted);

        // Risk indexes
        builder.Entity<EnterpriseRisk>().HasIndex(r => r.RiskNumber).IsUnique();
        builder.Entity<EnterpriseRisk>().HasIndex(r => r.RiskLevel);
        builder.Entity<EnterpriseRisk>().HasIndex(r => r.IsDeleted);
        builder.Entity<RiskCategory>().HasIndex(rc => rc.Code).IsUnique();

        // Feedback indexes
        builder.Entity<CustomerFeedback>().HasIndex(f => f.FeedbackNumber).IsUnique();
        builder.Entity<CustomerFeedback>().HasIndex(f => f.Status);
        builder.Entity<FeedbackCategory>().HasIndex(fc => fc.Code).IsUnique();

        // Improvement indexes — Status and Quadrant are hot filters on the
        // Kanban + Roadmap + Dashboard views, so index them explicitly.
        builder.Entity<ImprovementInitiative>().HasIndex(i => i.Code).IsUnique();
        builder.Entity<ImprovementInitiative>().HasIndex(i => i.Status);
        builder.Entity<ImprovementInitiative>().HasIndex(i => i.Quadrant);
        builder.Entity<ImprovementInitiative>().HasIndex(i => i.Horizon);
        builder.Entity<ImprovementInitiative>().HasIndex(i => i.IsDeleted);

        // Improvement Measurement indexes
        builder.Entity<ImprovementMeasurement>().HasIndex(m => m.ImprovementId);

        // MeasurementReading: cascade delete with parent measurement + unique
        // (MeasurementId, PeriodLabel) so the same period can't be entered twice.
        builder.Entity<MeasurementReading>(e =>
        {
            e.ToTable("MeasurementReadings");
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Measurement)
                .WithMany()
                .HasForeignKey(r => r.MeasurementId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => new { r.MeasurementId, r.PeriodLabel }).IsUnique();
            e.HasIndex(r => r.PeriodStart);
        });

        // ImprovementClosureReport: one per initiative, cascade delete
        builder.Entity<ImprovementClosureReport>(e =>
        {
            e.ToTable("ImprovementClosureReports");
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Improvement)
                .WithMany()
                .HasForeignKey(r => r.ImprovementId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.ImprovementId).IsUnique();
        });

        // ImprovementReview: many per initiative, cascade delete
        builder.Entity<ImprovementReview>(e =>
        {
            e.ToTable("ImprovementReviews");
            e.HasKey(r => r.Id);
            e.HasOne(r => r.Improvement)
                .WithMany()
                .HasForeignKey(r => r.ImprovementId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(r => r.ImprovementId);
            e.HasIndex(r => r.NextReviewDate);
        });

        // Notification dedup index — supports the 24h dedup window check in
        // NotificationService.SendAsync. Composite (UserId, DedupKey) so the
        // common query (this user, this key, unread, recent) lands on a
        // covering seek instead of a Notifications scan.
        builder.Entity<ESEMS.Web.Models.Notifications.Notification>()
            .HasIndex(n => new { n.UserId, n.DedupKey });

        // SLA indexes
        builder.Entity<SLADefinition>().HasIndex(s => s.Code).IsUnique();
        builder.Entity<SLABreach>().HasIndex(sb => sb.BreachNumber).IsUnique();
        builder.Entity<SLABreach>().HasIndex(sb => sb.BreachDate);
    }

    private void ConfigureServiceManagement(ModelBuilder builder)
    {
        // Incident -> Service
        builder.Entity<Incident>()
            .HasOne(i => i.Service)
            .WithMany(s => s.Incidents)
            .HasForeignKey(i => i.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Incident -> Process
        builder.Entity<Incident>()
            .HasOne(i => i.Process)
            .WithMany(p => p.Incidents)
            .HasForeignKey(i => i.ProcessId)
            .OnDelete(DeleteBehavior.SetNull);

        // Incident -> Asset
        builder.Entity<Incident>()
            .HasOne(i => i.Asset)
            .WithMany(a => a.Incidents)
            .HasForeignKey(i => i.AssetId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Incident>()
            .HasOne(i => i.Problem)
            .WithMany(p => p.RelatedIncidents)
            .HasForeignKey(i => i.ProblemId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Incident>()
            .HasOne(i => i.AssignedToUnit)
            .WithMany()
            .HasForeignKey(i => i.AssignedToUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        // Problem -> Service
        builder.Entity<Problem>()
            .HasOne(p => p.Service)
            .WithMany(s => s.Problems)
            .HasForeignKey(p => p.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        // Problem -> Process
        builder.Entity<Problem>()
            .HasOne(p => p.Process)
            .WithMany(p => p.Problems)
            .HasForeignKey(p => p.ProcessId)
            .OnDelete(DeleteBehavior.SetNull);

        // Problem -> Asset
        builder.Entity<Problem>()
            .HasOne(p => p.Asset)
            .WithMany(a => a.Problems)
            .HasForeignKey(p => p.AssetId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Problem>()
            .HasOne(p => p.AssignedToUnit)
            .WithMany()
            .HasForeignKey(p => p.AssignedToUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        // Comment relationships
        builder.Entity<IncidentComment>()
            .HasOne(c => c.Incident)
            .WithMany(i => i.Comments)
            .HasForeignKey(c => c.IncidentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ProblemComment>()
            .HasOne(c => c.Problem)
            .WithMany(p => p.Comments)
            .HasForeignKey(c => c.ProblemId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureAssetManagement(ModelBuilder builder)
    {
        // Asset relationships
        builder.Entity<Asset>()
            .HasOne(a => a.Category)
            .WithMany(c => c.Assets)
            .HasForeignKey(a => a.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // Asset -> Process
        builder.Entity<Asset>()
            .HasOne(a => a.Process)
            .WithMany(p => p.Assets)
            .HasForeignKey(a => a.ProcessId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Asset>()
            .HasOne(a => a.AssignedToUnit)
            .WithMany()
            .HasForeignKey(a => a.AssignedToUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        // AssetCategory self-referencing
        builder.Entity<AssetCategory>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // MaintenanceSchedule relationships
        builder.Entity<MaintenanceSchedule>()
            .HasOne(m => m.Asset)
            .WithMany(a => a.MaintenanceSchedules)
            .HasForeignKey(m => m.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        // MaintenanceRecord relationships
        builder.Entity<MaintenanceRecord>()
            .HasOne(m => m.Asset)
            .WithMany(a => a.MaintenanceRecords)
            .HasForeignKey(m => m.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MaintenanceRecord>()
            .HasOne(m => m.MaintenanceSchedule)
            .WithMany()
            .HasForeignKey(m => m.MaintenanceScheduleId)
            .OnDelete(DeleteBehavior.NoAction);

        // AssetRisk many-to-many relationship
        builder.Entity<AssetRisk>()
            .HasKey(ar => new { ar.AssetId, ar.RiskId });

        builder.Entity<AssetRisk>()
            .HasOne(ar => ar.Asset)
            .WithMany(a => a.AssetRisks)
            .HasForeignKey(ar => ar.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<AssetRisk>()
            .HasOne(ar => ar.Risk)
            .WithMany(r => r.AssetRisks)
            .HasForeignKey(ar => ar.RiskId)
            .OnDelete(DeleteBehavior.Cascade);

        // ServiceAsset many-to-many relationship
        builder.Entity<ServiceAsset>()
            .HasKey(sa => new { sa.ServiceId, sa.AssetId });

        builder.Entity<ServiceAsset>()
            .HasOne(sa => sa.Service)
            .WithMany(s => s.ServiceAssets)
            .HasForeignKey(sa => sa.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ServiceAsset>()
            .HasOne(sa => sa.Asset)
            .WithMany(a => a.ServiceAssets)
            .HasForeignKey(sa => sa.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        // ServiceRisk many-to-many relationship
        builder.Entity<ServiceRisk>()
            .HasKey(sr => new { sr.ServiceId, sr.RiskId });

        builder.Entity<ServiceRisk>()
            .HasOne(sr => sr.Service)
            .WithMany(s => s.ServiceRisks)
            .HasForeignKey(sr => sr.ServiceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ServiceRisk>()
            .HasOne(sr => sr.Risk)
            .WithMany(r => r.ServiceRisks)
            .HasForeignKey(sr => sr.RiskId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChangeRequestAsset many-to-many relationship
        builder.Entity<ChangeRequestAsset>()
            .HasKey(cra => new { cra.ChangeRequestId, cra.AssetId });

        builder.Entity<ChangeRequestAsset>()
            .HasOne(cra => cra.ChangeRequest)
            .WithMany(cr => cr.ChangeRequestAssets)
            .HasForeignKey(cra => cra.ChangeRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChangeRequestAsset>()
            .HasOne(cra => cra.Asset)
            .WithMany(a => a.ChangeRequestAssets)
            .HasForeignKey(cra => cra.AssetId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChangeRequestRisk many-to-many relationship
        builder.Entity<ChangeRequestRisk>()
            .HasKey(crr => new { crr.ChangeRequestId, crr.RiskId });

        builder.Entity<ChangeRequestRisk>()
            .HasOne(crr => crr.ChangeRequest)
            .WithMany(cr => cr.ChangeRequestRisks)
            .HasForeignKey(crr => crr.ChangeRequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ChangeRequestRisk>()
            .HasOne(crr => crr.Risk)
            .WithMany(r => r.ChangeRequestRisks)
            .HasForeignKey(crr => crr.RiskId)
            .OnDelete(DeleteBehavior.Cascade);

        // ImprovementRisk many-to-many relationship
        // Pattern mirrors PManagement's InitiativeRiskLink
        builder.Entity<ImprovementRisk>()
            .HasKey(ir => new { ir.ImprovementId, ir.RiskId });

        builder.Entity<ImprovementRisk>()
            .HasOne(ir => ir.Improvement)
            .WithMany(i => i.ImprovementRisks)
            .HasForeignKey(ir => ir.ImprovementId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<ImprovementRisk>()
            .HasOne(ir => ir.Risk)
            .WithMany(r => r.ImprovementRisks)
            .HasForeignKey(ir => ir.RiskId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    private void ConfigureRiskManagement(ModelBuilder builder)
    {
        // EnterpriseRisk relationships
        builder.Entity<EnterpriseRisk>()
            .HasOne(r => r.Category)
            .WithMany(c => c.Risks)
            .HasForeignKey(r => r.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // EnterpriseRisk -> Process
        builder.Entity<EnterpriseRisk>()
            .HasOne(r => r.Process)
            .WithMany(p => p.EnterpriseRisks)
            .HasForeignKey(r => r.ProcessId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<EnterpriseRisk>()
            .HasOne(r => r.OrganizationUnit)
            .WithMany()
            .HasForeignKey(r => r.OrganizationUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        // RiskCategory self-referencing
        builder.Entity<RiskCategory>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // RiskActionPlan relationships
        builder.Entity<RiskActionPlan>()
            .HasOne(a => a.Risk)
            .WithMany(r => r.ActionPlans)
            .HasForeignKey(a => a.RiskId)
            .OnDelete(DeleteBehavior.Cascade);

        // Store RiskActionPlanStatus as string so existing free-text rows
        // ("Not Started", "In Progress", etc.) migrate cleanly via the
        // TightenRiskActionPlanAudit migration's data-fix UPDATE.
        builder.Entity<RiskActionPlan>()
            .Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        // RiskActionPlan now inherits AuditableBilingualEntity, so the
        // global IsDeleted query filter applies — match the convention used
        // by every other AuditableBilingualEntity table.
        builder.Entity<RiskActionPlan>().HasIndex(a => a.IsDeleted);
    }

    private void ConfigureCustomerFeedback(ModelBuilder builder)
    {
        // CustomerFeedback relationships
        builder.Entity<CustomerFeedback>()
            .HasOne(f => f.Category)
            .WithMany(c => c.Feedbacks)
            .HasForeignKey(f => f.CategoryId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<CustomerFeedback>()
            .HasOne(f => f.Service)
            .WithMany()
            .HasForeignKey(f => f.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<CustomerFeedback>()
            .HasOne(f => f.Process)
            .WithMany()
            .HasForeignKey(f => f.ProcessId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<CustomerFeedback>()
            .HasOne(f => f.AssignedToUnit)
            .WithMany()
            .HasForeignKey(f => f.AssignedToUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        // FeedbackCategory self-referencing
        builder.Entity<FeedbackCategory>()
            .HasOne(c => c.ParentCategory)
            .WithMany(c => c.SubCategories)
            .HasForeignKey(c => c.ParentCategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<FeedbackCategory>()
            .HasOne(c => c.DefaultAssignedToUnit)
            .WithMany()
            .HasForeignKey(c => c.DefaultAssignedToUnitId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureSLAMonitoring(ModelBuilder builder)
    {
        // SLADefinition relationships
        builder.Entity<SLADefinition>()
            .HasOne(s => s.Service)
            .WithMany()
            .HasForeignKey(s => s.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<SLADefinition>()
            .HasOne(s => s.ResponsibleUnit)
            .WithMany()
            .HasForeignKey(s => s.ResponsibleUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        // SLABreach relationships
        builder.Entity<SLABreach>()
            .HasOne(b => b.SLADefinition)
            .WithMany(s => s.Breaches)
            .HasForeignKey(b => b.SLADefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SLABreach>()
            .HasOne(b => b.Incident)
            .WithMany()
            .HasForeignKey(b => b.IncidentId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    private void ConfigureBpmnVersionHistory(ModelBuilder builder)
    {
        // ProcessBpmnVersion -> Process
        builder.Entity<ProcessBpmnVersion>()
            .HasOne(v => v.Process)
            .WithMany()
            .HasForeignKey(v => v.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        // ProcessBpmnVersion -> User (CreatedBy) - User is [NotMapped], so no EF relationship
        // CreatedById is stored as a string FK but not enforced by EF

        // Indexes for efficient queries
        builder.Entity<ProcessBpmnVersion>()
            .HasIndex(v => v.ProcessId);

        builder.Entity<ProcessBpmnVersion>()
            .HasIndex(v => v.CreatedAt);

        builder.Entity<ProcessBpmnVersion>()
            .HasIndex(v => new { v.ProcessId, v.VersionNumber })
            .IsUnique();

        // BpmnLane -> Process (cascade so deleting a process clears its lanes)
        builder.Entity<BpmnLane>()
            .HasOne(l => l.Process)
            .WithMany()
            .HasForeignKey(l => l.ProcessId)
            .OnDelete(DeleteBehavior.Cascade);

        // BpmnLane -> OrganizationUnit (Restrict — don't let a unit deletion
        // silently break lane links; force the operator to re-reconcile)
        builder.Entity<BpmnLane>()
            .HasOne(l => l.OrganizationUnit)
            .WithMany()
            .HasForeignKey(l => l.OrganizationUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        // One row per (Process, BpmnId) — re-import overwrites in place
        builder.Entity<BpmnLane>()
            .HasIndex(l => new { l.ProcessId, l.BpmnId })
            .IsUnique();

        // Cheap lookup for the review queue ("unreconciled lanes")
        builder.Entity<BpmnLane>()
            .HasIndex(l => l.OrganizationUnitId);
    }

    private void ConfigureCustomUserManagement(ModelBuilder builder)
    {
        // CustomUser configuration
        builder.Entity<CustomUser>(entity =>
        {
            entity.ToTable("user");
            entity.HasKey(e => e.UserId);

            // Unique constraint on username
            entity.HasIndex(e => e.Username).IsUnique();

            // Organization Unit relationship (now the merged business OrganizationUnit)
            entity.HasOne(e => e.OrganizationUnit)
                .WithMany()
                .HasForeignKey(e => e.UnitId)
                .OnDelete(DeleteBehavior.SetNull);

            // Manager relationship (self-referencing)
            entity.HasOne(e => e.Manager)
                .WithMany()
                .HasForeignKey(e => e.DirectManager)
                .OnDelete(DeleteBehavior.NoAction);
        });

        // Plan X: UserRoleGroup junction — matrix-driven authorization
        builder.Entity<Models.Common.UserRoleGroup>(entity =>
        {
            entity.ToTable("UserRoleGroups");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.RoleGroup)
                .WithMany()
                .HasForeignKey(e => e.RoleGroupId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => new { e.UserId, e.RoleGroupId }).IsUnique();
        });
    }

    /// <summary>
    /// Configures the My Space (per-user document library) and the
    /// Process → UserDocument link table used by the Document Linking UI.
    /// </summary>
    private void ConfigureDocumentLinking(ModelBuilder builder)
    {
        // UserDocument → CustomUser
        builder.Entity<UserDocument>(entity =>
        {
            entity.ToTable("UserDocuments");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.User)
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsDeleted);
        });

        // ProcessDocument join: Process ↔ UserDocument with per-link metadata
        builder.Entity<ProcessDocument>(entity =>
        {
            entity.ToTable("ProcessDocuments");
            entity.HasKey(e => e.Id);

            entity.HasOne(e => e.Process)
                .WithMany()
                .HasForeignKey(e => e.ProcessId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.UserDocument)
                .WithMany()
                .HasForeignKey(e => e.UserDocumentId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.DocumentCategory)
                .WithMany()
                .HasForeignKey(e => e.DocumentCategoryId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasOne(e => e.DocumentType)
                .WithMany()
                .HasForeignKey(e => e.DocumentTypeId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.ProcessId);
            entity.HasIndex(e => e.UserDocumentId);
        });
    }

    private void ConfigureWorkloadAnalysis(ModelBuilder builder)
    {
        // WorkloadConfig → OrganizationUnit (optional, null = global default)
        builder.Entity<WorkloadConfig>()
            .HasOne(c => c.OrganizationUnit)
            .WithMany()
            .HasForeignKey(c => c.OrganizationUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<WorkloadConfig>()
            .HasIndex(c => c.OrganizationUnitId);

        // WorkloadScenario → WorkloadConfig
        builder.Entity<WorkloadScenario>()
            .HasOne(s => s.Config)
            .WithMany()
            .HasForeignKey(s => s.WorkloadConfigId)
            .OnDelete(DeleteBehavior.Restrict);

        // WorkloadScenario → OrganizationUnit (scoping)
        builder.Entity<WorkloadScenario>()
            .HasOne(s => s.OwningUnit)
            .WithMany()
            .HasForeignKey(s => s.OwningUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<WorkloadScenario>()
            .HasIndex(s => s.Code).IsUnique();

        builder.Entity<WorkloadScenario>()
            .HasIndex(s => new { s.FiscalYear, s.OwningUnitId });

        // WorkloadLineItem → WorkloadScenario (cascade)
        builder.Entity<WorkloadLineItem>()
            .HasOne(li => li.Scenario)
            .WithMany(s => s.LineItems)
            .HasForeignKey(li => li.WorkloadScenarioId)
            .OnDelete(DeleteBehavior.Cascade);

        // WorkloadLineItem → Process (optional)
        builder.Entity<WorkloadLineItem>()
            .HasOne(li => li.Process)
            .WithMany()
            .HasForeignKey(li => li.ProcessId)
            .OnDelete(DeleteBehavior.SetNull);

        // WorkloadLineItem → Service (optional)
        builder.Entity<WorkloadLineItem>()
            .HasOne(li => li.Service)
            .WithMany()
            .HasForeignKey(li => li.ServiceId)
            .OnDelete(DeleteBehavior.SetNull);
    }

    /// <summary>
    /// Explicit decimal precision for every numeric column. Without these EF
    /// defaults to decimal(18,2) which silently truncates fractional KPI
    /// values, FTE math, and high-precision ratios. The audit flagged this
    /// as Critical because TotalFTE — the headcount number reported to DGEP
    /// — is computed from these decimals and loses precision at every
    /// multiply.
    ///
    /// Precision matrix:
    ///   • AED currency           → decimal(18, 2) — supports billions of AED to fils
    ///   • Areas (sqm)            → decimal(12, 2) — up to 10B sqm
    ///   • Hours / days / minutes → decimal( 8, 2) — 99,999.99 headroom
    ///   • Aggregated minutes     → decimal(12, 2) — sum-of-children inflation safe
    ///   • KPI target/actual      → decimal(18, 4) — flexible for %, ratios, counts
    ///   • Percentage (0–100)     → decimal( 5, 2) — room for "120%" anomalies
    ///   • Maturity scores (0–5)  → decimal( 4, 2)
    ///   • Multipliers (1.0–9.9)  → decimal( 4, 2)
    ///   • Ratio (0–1)            → decimal( 5, 4) — utilization rates
    /// </summary>
    private static void ConfigureDecimalPrecisions(ModelBuilder builder)
    {
        // ── MeasurableEntity-derived (Process / Activity / ProcessTask /
        //    Service) share Estimated/Actual Cost & Duration columns ──────
        foreach (var e in new[] { typeof(Process), typeof(Activity), typeof(ProcessTask), typeof(Service) })
        {
            builder.Entity(e).Property("EstimatedCost").HasPrecision(18, 2);
            builder.Entity(e).Property("ActualCost").HasPrecision(18, 2);
            builder.Entity(e).Property("EstimatedDuration").HasPrecision(8, 2);
            builder.Entity(e).Property("ActualDuration").HasPrecision(8, 2);
        }

        // ── APQC hierarchy aggregates (sum-of-children) ──────────────────
        builder.Entity<Category>().Property(c => c.AggregatedCost).HasPrecision(18, 2);
        builder.Entity<Category>().Property(c => c.AggregatedDurationMinutes).HasPrecision(12, 2);
        builder.Entity<ProcessGroup>().Property(p => p.AggregatedCost).HasPrecision(18, 2);
        builder.Entity<ProcessGroup>().Property(p => p.AggregatedDurationMinutes).HasPrecision(12, 2);
        builder.Entity<Process>().Property(p => p.AggregatedCost).HasPrecision(18, 2);
        builder.Entity<Process>().Property(p => p.AggregatedDurationMinutes).HasPrecision(12, 2);
        builder.Entity<Activity>().Property(a => a.AggregatedCost).HasPrecision(18, 2);
        builder.Entity<Activity>().Property(a => a.AggregatedDurationMinutes).HasPrecision(12, 2);

        // ── Service (delivery + financials + CSAT) ───────────────────────
        builder.Entity<Service>().Property(s => s.TargetDeliveryDays).HasPrecision(8, 2);
        builder.Entity<Service>().Property(s => s.ActualDeliveryDays).HasPrecision(8, 2);
        builder.Entity<Service>().Property(s => s.ServiceFee).HasPrecision(18, 2);
        builder.Entity<Service>().Property(s => s.CustomerSatisfactionScore).HasPrecision(5, 2);

        // ── ServiceCatalogInfo (citizen-facing duration + fee) ───────────
        builder.Entity<ServiceCatalogInfo>().Property(c => c.DurationValue).HasPrecision(8, 2);
        builder.Entity<ServiceCatalogInfo>().Property(c => c.FeeAmount).HasPrecision(18, 2);

        // ── KPI / measurement values (Service + Process measurements) ───
        builder.Entity<ServiceMeasurement>().Property(m => m.TargetValue).HasPrecision(18, 4);
        builder.Entity<ServiceMeasurement>().Property(m => m.ActualValue).HasPrecision(18, 4);
        builder.Entity<ServiceMeasurement>().Property(m => m.MinValue).HasPrecision(18, 4);
        builder.Entity<ServiceMeasurement>().Property(m => m.MaxValue).HasPrecision(18, 4);
        builder.Entity<ProcessMeasurement>().Property(m => m.TargetValue).HasPrecision(18, 4);
        builder.Entity<ProcessMeasurement>().Property(m => m.ActualValue).HasPrecision(18, 4);
        builder.Entity<ProcessMeasurement>().Property(m => m.MinValue).HasPrecision(18, 4);
        builder.Entity<ProcessMeasurement>().Property(m => m.MaxValue).HasPrecision(18, 4);
        builder.Entity<StrategicObjective>().Property(o => o.TargetValue).HasPrecision(18, 4);
        builder.Entity<StrategicObjective>().Property(o => o.CurrentValue).HasPrecision(18, 4);

        // ── Financial / currency (AED) ────────────────────────────────────
        builder.Entity<SystemDefinition>().Property(s => s.AnnualLicenseCost).HasPrecision(18, 2);
        builder.Entity<Models.Workflow.ApprovalConfiguration>().Property(a => a.MinCostSavings).HasPrecision(18, 2);
        builder.Entity<Models.Workflow.ApprovalConfiguration>().Property(a => a.MaxCostSavings).HasPrecision(18, 2);

        // ── Asset (financial + real-estate area) ─────────────────────────
        builder.Entity<Asset>().Property(a => a.PurchaseCost).HasPrecision(18, 2);
        builder.Entity<Asset>().Property(a => a.CurrentValue).HasPrecision(18, 2);
        builder.Entity<Asset>().Property(a => a.DepreciationRate).HasPrecision(5, 2);
        builder.Entity<Asset>().Property(a => a.BuiltUpAreaSqm).HasPrecision(12, 2);
        builder.Entity<Asset>().Property(a => a.LandAreaSqm).HasPrecision(12, 2);
        builder.Entity<AssetCategory>().Property(c => c.DefaultDepreciationRate).HasPrecision(5, 2);

        // ── Maintenance (cost + hours) ───────────────────────────────────
        builder.Entity<MaintenanceSchedule>().Property(m => m.EstimatedCost).HasPrecision(18, 2);
        builder.Entity<MaintenanceSchedule>().Property(m => m.EstimatedDurationHours).HasPrecision(8, 2);
        builder.Entity<MaintenanceRecord>().Property(m => m.Cost).HasPrecision(18, 2);
        builder.Entity<MaintenanceRecord>().Property(m => m.DurationHours).HasPrecision(8, 2);
        builder.Entity<MaintenanceRecord>().Property(m => m.DowntimeHours).HasPrecision(8, 2);

        // ── Improvement / benefits realisation ───────────────────────────
        builder.Entity<ImprovementInitiative>().Property(i => i.EstimatedCostSavings).HasPrecision(18, 2);
        builder.Entity<ImprovementInitiative>().Property(i => i.ActualCostSavings).HasPrecision(18, 2);
        builder.Entity<ImprovementInitiative>().Property(i => i.EstimatedTimeSavings).HasPrecision(8, 2);
        builder.Entity<ImprovementInitiative>().Property(i => i.ActualTimeSavings).HasPrecision(8, 2);
        builder.Entity<ImprovementBenefitsReview>().Property(b => b.ActualCostSaving).HasPrecision(18, 2);
        builder.Entity<ImprovementBenefitsReview>().Property(b => b.ActualTimeSaving).HasPrecision(8, 2);
        builder.Entity<ImprovementMeasurement>().Property(m => m.AsIsValue).HasPrecision(18, 4);
        builder.Entity<ImprovementMeasurement>().Property(m => m.TargetValue).HasPrecision(18, 4);
        builder.Entity<ImprovementMeasurement>().Property(m => m.ToBeValue).HasPrecision(18, 4);
        builder.Entity<MeasurementReading>().Property(r => r.Value).HasPrecision(18, 4);

        // ── Process Maturity Assessment (8 BPM scores, 0-5 scale) ────────
        builder.Entity<ProcessMaturityAssessment>().Property(m => m.OverallScore).HasPrecision(4, 2);
        builder.Entity<ProcessMaturityAssessment>().Property(m => m.StrategicAlignment).HasPrecision(4, 2);
        builder.Entity<ProcessMaturityAssessment>().Property(m => m.Governance).HasPrecision(4, 2);
        builder.Entity<ProcessMaturityAssessment>().Property(m => m.ProcessModels).HasPrecision(4, 2);
        builder.Entity<ProcessMaturityAssessment>().Property(m => m.ProcessImprovement).HasPrecision(4, 2);
        builder.Entity<ProcessMaturityAssessment>().Property(m => m.ProcessPerformance).HasPrecision(4, 2);
        builder.Entity<ProcessMaturityAssessment>().Property(m => m.ChangeManagement).HasPrecision(4, 2);
        builder.Entity<ProcessMaturityAssessment>().Property(m => m.ToolsAndTechnology).HasPrecision(4, 2);

        // ── Problem (cost impact) ────────────────────────────────────────
        builder.Entity<Problem>().Property(p => p.EstimatedCostImpact).HasPrecision(18, 2);
        builder.Entity<Problem>().Property(p => p.ActualCostImpact).HasPrecision(18, 2);

        // ── Risk action plan ─────────────────────────────────────────────
        builder.Entity<Models.RiskManagement.RiskActionPlan>().Property(p => p.EstimatedCost).HasPrecision(18, 2);
        builder.Entity<Models.RiskManagement.RiskActionPlan>().Property(p => p.ActualCost).HasPrecision(18, 2);

        // ── SLA monitoring ──────────────────────────────────────────────
        builder.Entity<Models.SLA.SLADefinition>().Property(s => s.TargetValue).HasPrecision(18, 4);
        builder.Entity<Models.SLA.SLADefinition>().Property(s => s.WarningThreshold).HasPrecision(18, 4);
        builder.Entity<Models.SLA.SLABreach>().Property(b => b.TargetValue).HasPrecision(18, 4);
        builder.Entity<Models.SLA.SLABreach>().Property(b => b.ActualValue).HasPrecision(18, 4);
        builder.Entity<Models.SLA.SLABreach>().Property(b => b.Variance).HasPrecision(18, 4);
        builder.Entity<Models.SLA.SLABreach>().Property(b => b.VariancePercentage).HasPrecision(5, 2);
        builder.Entity<Models.SLA.SLABreach>().Property(b => b.FinancialImpact).HasPrecision(18, 2);

        // ── Legacy KPITrend table (drop candidate per audit) ─────────────
        builder.Entity<KPITrend>().Property(k => k.TargetValue).HasPrecision(18, 4);
        builder.Entity<KPITrend>().Property(k => k.Value).HasPrecision(18, 4);

        // ── Workload modelling — every decimal feeds into FTE math ───────
        builder.Entity<WorkloadConfig>().Property(c => c.WorkingHoursPerDay).HasPrecision(5, 2);
        builder.Entity<WorkloadConfig>().Property(c => c.AdminOverheadPercent).HasPrecision(5, 2);
        builder.Entity<WorkloadConfig>().Property(c => c.TargetUtilizationRate).HasPrecision(5, 4);

        builder.Entity<WorkloadScenario>().Property(s => s.GrowthRatePercent).HasPrecision(5, 2);

        builder.Entity<WorkloadLineItem>().Property(l => l.AvgProcessingTimeMinutes).HasPrecision(8, 2);
        builder.Entity<WorkloadLineItem>().Property(l => l.SimpleMult).HasPrecision(4, 2);
        builder.Entity<WorkloadLineItem>().Property(l => l.MediumMult).HasPrecision(4, 2);
        builder.Entity<WorkloadLineItem>().Property(l => l.ComplexMult).HasPrecision(4, 2);
        builder.Entity<WorkloadLineItem>().Property(l => l.SimpleVolumePercent).HasPrecision(5, 2);
        builder.Entity<WorkloadLineItem>().Property(l => l.MediumVolumePercent).HasPrecision(5, 2);
        builder.Entity<WorkloadLineItem>().Property(l => l.ComplexVolumePercent).HasPrecision(5, 2);
    }
}
