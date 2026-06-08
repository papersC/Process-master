using Microsoft.EntityFrameworkCore;

namespace ESEMS.Web.Data;

/// <summary>
/// Ensures schema that EF migrations expect but that was originally added outside EF.
/// <see cref="EnsureSchemaAsync"/> runs only before <see cref="DatabaseFacade.MigrateAsync"/>
/// and must not touch tables created by <c>InitialCustomUserTables</c> (they do not exist yet
/// on a greenfield database). Dependent objects are ensured inside the migrations that need them.
/// </summary>
public static class PreMigrationBootstrap
{
    /// <summary>SQL run at the start of <c>SetDecimalPrecisions</c> (after Initial migration).</summary>
    public const string SetDecimalPrecisionsPrerequisitesSql = """
        IF OBJECT_ID('dbo.ImprovementMeasurements', 'U') IS NOT NULL
        AND NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'MeasurementReadings')
        BEGIN
            CREATE TABLE [dbo].[MeasurementReadings] (
                [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                [MeasurementId] NVARCHAR(450) NOT NULL,
                [PeriodLabel] NVARCHAR(30) NOT NULL,
                [PeriodStart] DATETIME2 NOT NULL,
                [Value] DECIMAL(18, 4) NULL,
                [Notes] NVARCHAR(1000) NULL,
                [EnteredById] INT NULL,
                [EnteredAt] DATETIME2 NULL,
                [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                CONSTRAINT [FK_MeasurementReadings_ImprovementMeasurements]
                    FOREIGN KEY ([MeasurementId]) REFERENCES [dbo].[ImprovementMeasurements] ([Id]) ON DELETE CASCADE
            );
            CREATE UNIQUE INDEX [IX_MeasurementReadings_Measurement_Period]
                ON [dbo].[MeasurementReadings] ([MeasurementId], [PeriodLabel]);
            CREATE INDEX [IX_MeasurementReadings_PeriodStart]
                ON [dbo].[MeasurementReadings] ([PeriodStart]);
        END;

        IF OBJECT_ID('dbo.OrganizationUnits', 'U') IS NOT NULL
        AND NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkloadConfigs')
        BEGIN
            CREATE TABLE [dbo].[WorkloadConfigs] (
                [Id]                     NVARCHAR(450) NOT NULL PRIMARY KEY,
                [NameEn]                 NVARCHAR(MAX) NOT NULL DEFAULT '',
                [NameAr]                 NVARCHAR(MAX) NOT NULL DEFAULT '',
                [DescriptionEn]          NVARCHAR(MAX) NULL,
                [DescriptionAr]          NVARCHAR(MAX) NULL,
                [WorkingHoursPerDay]     DECIMAL(18,2) NOT NULL DEFAULT 7.5,
                [WorkingDaysPerWeek]     INT NOT NULL DEFAULT 5,
                [PublicHolidaysPerYear]  INT NOT NULL DEFAULT 12,
                [AnnualLeaveDays]        INT NOT NULL DEFAULT 22,
                [AverageSickDays]        INT NOT NULL DEFAULT 7,
                [TrainingDaysPerYear]    INT NOT NULL DEFAULT 7,
                [AdminOverheadPercent]   DECIMAL(18,2) NOT NULL DEFAULT 15,
                [TargetUtilizationRate]  DECIMAL(18,2) NOT NULL DEFAULT 0.80,
                [SupervisoryRatio]       INT NOT NULL DEFAULT 8,
                [OrganizationUnitId]     NVARCHAR(450) NULL,
                [FiscalYearStart]        INT NOT NULL DEFAULT 1,
                [CreatedAt]              DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]              DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedById]            NVARCHAR(MAX) NULL,
                [UpdatedById]            NVARCHAR(MAX) NULL,
                [Version]                INT NOT NULL DEFAULT 1,
                [IsDeleted]              BIT NOT NULL DEFAULT 0,
                [DeletedAt]              DATETIME2 NULL,
                CONSTRAINT [FK_WorkloadConfigs_OrgUnit] FOREIGN KEY ([OrganizationUnitId])
                    REFERENCES [dbo].[OrganizationUnits]([Id]) ON DELETE SET NULL
            );
            CREATE INDEX [IX_WorkloadConfigs_OrganizationUnitId] ON [dbo].[WorkloadConfigs]([OrganizationUnitId]);
        END;

        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkloadConfigs')
        AND OBJECT_ID('dbo.OrganizationUnits', 'U') IS NOT NULL
        AND NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkloadScenarios')
        BEGIN
            CREATE TABLE [dbo].[WorkloadScenarios] (
                [Id]                  NVARCHAR(450) NOT NULL PRIMARY KEY,
                [NameEn]              NVARCHAR(MAX) NOT NULL DEFAULT '',
                [NameAr]              NVARCHAR(MAX) NOT NULL DEFAULT '',
                [DescriptionEn]       NVARCHAR(MAX) NULL,
                [DescriptionAr]       NVARCHAR(MAX) NULL,
                [Code]                NVARCHAR(450) NOT NULL DEFAULT '',
                [Status]              INT NOT NULL DEFAULT 0,
                [FiscalYear]          INT NOT NULL,
                [OwningUnitId]        NVARCHAR(450) NULL,
                [WorkloadConfigId]    NVARCHAR(450) NOT NULL,
                [GrowthRatePercent]   DECIMAL(18,2) NULL,
                [ProjectionYears]     INT NOT NULL DEFAULT 0,
                [CurrentHeadcount]    INT NULL,
                [Notes]               NVARCHAR(MAX) NULL,
                [CreatedAt]           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]           DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedById]         NVARCHAR(MAX) NULL,
                [UpdatedById]         NVARCHAR(MAX) NULL,
                [Version]             INT NOT NULL DEFAULT 1,
                [IsDeleted]           BIT NOT NULL DEFAULT 0,
                [DeletedAt]           DATETIME2 NULL,
                CONSTRAINT [FK_WorkloadScenarios_Config] FOREIGN KEY ([WorkloadConfigId])
                    REFERENCES [dbo].[WorkloadConfigs]([Id]),
                CONSTRAINT [FK_WorkloadScenarios_OrgUnit] FOREIGN KEY ([OwningUnitId])
                    REFERENCES [dbo].[OrganizationUnits]([Id]) ON DELETE SET NULL
            );
            CREATE UNIQUE INDEX [IX_WorkloadScenarios_Code] ON [dbo].[WorkloadScenarios]([Code]);
            CREATE INDEX [IX_WorkloadScenarios_FiscalYear_Unit] ON [dbo].[WorkloadScenarios]([FiscalYear], [OwningUnitId]);
        END;

        IF EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkloadScenarios')
        AND NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkloadLineItems')
        BEGIN
            CREATE TABLE [dbo].[WorkloadLineItems] (
                [Id]                       NVARCHAR(450) NOT NULL PRIMARY KEY,
                [NameEn]                   NVARCHAR(MAX) NOT NULL DEFAULT '',
                [NameAr]                   NVARCHAR(MAX) NOT NULL DEFAULT '',
                [DescriptionEn]            NVARCHAR(MAX) NULL,
                [DescriptionAr]            NVARCHAR(MAX) NULL,
                [WorkloadScenarioId]       NVARCHAR(450) NOT NULL,
                [ProcessId]                NVARCHAR(450) NULL,
                [ServiceId]                NVARCHAR(450) NULL,
                [AnnualVolume]             INT NOT NULL DEFAULT 0,
                [AvgProcessingTimeMinutes] DECIMAL(18,2) NOT NULL DEFAULT 0,
                [ComplexityEnabled]        BIT NOT NULL DEFAULT 0,
                [SimpleVolumePercent]      DECIMAL(18,2) NULL DEFAULT 100,
                [MediumVolumePercent]      DECIMAL(18,2) NULL,
                [ComplexVolumePercent]     DECIMAL(18,2) NULL,
                [SimpleMult]               DECIMAL(18,2) NULL DEFAULT 1.0,
                [MediumMult]               DECIMAL(18,2) NULL DEFAULT 1.5,
                [ComplexMult]              DECIMAL(18,2) NULL DEFAULT 2.5,
                [SeasonalDistribution]     NVARCHAR(MAX) NULL,
                [Notes]                    NVARCHAR(MAX) NULL,
                [CreatedAt]                DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [UpdatedAt]                DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                [CreatedById]              NVARCHAR(MAX) NULL,
                [UpdatedById]              NVARCHAR(MAX) NULL,
                [Version]                  INT NOT NULL DEFAULT 1,
                [IsDeleted]                BIT NOT NULL DEFAULT 0,
                [DeletedAt]                DATETIME2 NULL,
                CONSTRAINT [FK_WorkloadLineItems_Scenario] FOREIGN KEY ([WorkloadScenarioId])
                    REFERENCES [dbo].[WorkloadScenarios]([Id]) ON DELETE CASCADE
            );
            IF OBJECT_ID('dbo.Processes', 'U') IS NOT NULL
                ALTER TABLE [dbo].[WorkloadLineItems] ADD CONSTRAINT [FK_WorkloadLineItems_Process]
                    FOREIGN KEY ([ProcessId]) REFERENCES [dbo].[Processes]([Id]) ON DELETE SET NULL;
            IF OBJECT_ID('dbo.Services', 'U') IS NOT NULL
                ALTER TABLE [dbo].[WorkloadLineItems] ADD CONSTRAINT [FK_WorkloadLineItems_Service]
                    FOREIGN KEY ([ServiceId]) REFERENCES [dbo].[Services]([Id]) ON DELETE SET NULL;
        END;
        """;

    public static async Task EnsureSchemaAsync(ApplicationDbContext context, CancellationToken cancellationToken = default)
    {
        var db = context.Database;

        // ApprovalSlaAndDelegation (20260418) — not created by any EF migration
        await db.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowInstances')
            BEGIN
                CREATE TABLE [dbo].[WorkflowInstances] (
                    [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                    [Type] NVARCHAR(100) NOT NULL DEFAULT '',
                    [Status] INT NOT NULL DEFAULT 0,
                    [EntityId] NVARCHAR(450) NOT NULL DEFAULT '',
                    [EntityType] NVARCHAR(100) NOT NULL DEFAULT '',
                    [SubmittedById] INT NULL,
                    [SubmitterName] NVARCHAR(MAX) NULL,
                    [SubmittedAt] DATETIME2 NULL,
                    [CurrentLevel] INT NOT NULL DEFAULT 0,
                    [MaxLevel] INT NOT NULL DEFAULT 1,
                    [ApproverUserId] INT NULL,
                    [Notes] NVARCHAR(MAX) NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
            END
            """, cancellationToken);

        await db.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'WorkflowSteps')
            BEGIN
                CREATE TABLE [dbo].[WorkflowSteps] (
                    [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                    [WorkflowInstanceId] NVARCHAR(450) NOT NULL,
                    [StepLevel] INT NOT NULL DEFAULT 0,
                    [ApproverUserId] INT NULL,
                    [ApproverName] NVARCHAR(MAX) NULL,
                    [Action] NVARCHAR(50) NOT NULL DEFAULT 'Pending',
                    [Comments] NVARCHAR(MAX) NULL,
                    [ActionDate] DATETIME2 NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT [FK_WorkflowSteps_WorkflowInstances] FOREIGN KEY ([WorkflowInstanceId])
                        REFERENCES [dbo].[WorkflowInstances] ([Id]) ON DELETE CASCADE
                );
                CREATE INDEX [IX_WorkflowSteps_WorkflowInstanceId] ON [dbo].[WorkflowSteps] ([WorkflowInstanceId]);
            END
            """, cancellationToken);

        await db.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ApprovalConfigurations')
            BEGIN
                CREATE TABLE [dbo].[ApprovalConfigurations] (
                    [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                    [EntityType] NVARCHAR(100) NOT NULL DEFAULT '',
                    [Level1Required] BIT NOT NULL DEFAULT 1,
                    [Level1ApproverType] NVARCHAR(50) NOT NULL DEFAULT 'SpecificUser',
                    [Level1ApproverUserId] INT NULL,
                    [Level1ApproverName] NVARCHAR(MAX) NULL,
                    [Level2Required] BIT NOT NULL DEFAULT 0,
                    [Level2ApproverType] NVARCHAR(50) NULL,
                    [Level2ApproverUserId] INT NULL,
                    [Level2ApproverName] NVARCHAR(MAX) NULL,
                    [IsActive] BIT NOT NULL DEFAULT 1,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
            END
            """, cancellationToken);

        // AddNotificationDedupKey (20260516) — not created by any EF migration
        await db.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Notifications')
            BEGIN
                CREATE TABLE [dbo].[Notifications] (
                    [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                    [UserId] INT NULL,
                    [TitleEn] NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [TitleAr] NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [MessageEn] NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [MessageAr] NVARCHAR(MAX) NOT NULL DEFAULT '',
                    [Type] NVARCHAR(50) NOT NULL DEFAULT 'Info',
                    [IsRead] BIT NOT NULL DEFAULT 0,
                    [ReadAt] DATETIME2 NULL,
                    [RelatedEntityId] NVARCHAR(450) NULL,
                    [RelatedEntityType] NVARCHAR(100) NULL,
                    [ActionUrl] NVARCHAR(MAX) NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
                CREATE INDEX [IX_Notifications_UserId] ON [dbo].[Notifications] ([UserId]);
            END
            """, cancellationToken);

        // RoleGroups + UserRoleGroups (Plan X RBAC). These are created by raw
        // SQL in Program.cs that runs AFTER migrations, but the
        // DropLegacyRoleTables migration (20260526093839) INSERTs the four
        // parity role groups DURING MigrateAsync. On a greenfield database that
        // INSERT hit a table that did not exist yet — startup died with
        // "Invalid object name 'dbo.RoleGroups'". Create both here, before
        // MigrateAsync, so the migration's seed + backfill find them. Idempotent
        // (IF NOT EXISTS) — a no-op on any DB where Program.cs already made them.
        // RoleGroups carries IsSystemRole + Code inline because the migration's
        // INSERT sets those columns (Program.cs adds them via a later ALTER).
        // UserRoleGroups FKs only to RoleGroups (no user-table dependency — see
        // Program.cs), so it is safe to create this early on a greenfield DB.
        await db.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'RoleGroups')
            BEGIN
                CREATE TABLE [dbo].[RoleGroups] (
                    [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                    [NameEn] NVARCHAR(200) NOT NULL DEFAULT '',
                    [NameAr] NVARCHAR(200) NOT NULL DEFAULT '',
                    [DescriptionEn] NVARCHAR(500) NULL,
                    [DescriptionAr] NVARCHAR(500) NULL,
                    [ScopeLevel] NVARCHAR(50) NOT NULL DEFAULT 'All',
                    [Permissions] NVARCHAR(MAX) NULL,
                    [Icon] NVARCHAR(100) NOT NULL DEFAULT 'users',
                    [Color] NVARCHAR(20) NOT NULL DEFAULT '#005B99',
                    [IsActive] BIT NOT NULL DEFAULT 1,
                    [MemberCount] INT NOT NULL DEFAULT 0,
                    [IsSystemRole] BIT NOT NULL DEFAULT 0,
                    [Code] NVARCHAR(100) NULL,
                    [CreatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    [UpdatedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE()
                );
            END
            """, cancellationToken);

        await db.ExecuteSqlRawAsync("""
            IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'UserRoleGroups')
            BEGIN
                CREATE TABLE [dbo].[UserRoleGroups] (
                    [Id] NVARCHAR(450) NOT NULL PRIMARY KEY,
                    [UserId] INT NOT NULL,
                    [RoleGroupId] NVARCHAR(450) NOT NULL,
                    [AssignedBy] INT NULL,
                    [AssignedAt] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
                    CONSTRAINT [FK_UserRoleGroups_RoleGroups]
                        FOREIGN KEY ([RoleGroupId]) REFERENCES [dbo].[RoleGroups] ([Id]) ON DELETE CASCADE
                );
                CREATE UNIQUE INDEX [IX_UserRoleGroups_User_Group] ON [dbo].[UserRoleGroups] ([UserId], [RoleGroupId]);
                CREATE INDEX [IX_UserRoleGroups_UserId] ON [dbo].[UserRoleGroups] ([UserId]);
            END
            """, cancellationToken);
    }
}
