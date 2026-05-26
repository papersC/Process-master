IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [AssetCategories] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [ParentCategoryId] nvarchar(450) NULL,
        [DefaultDepreciationRate] decimal(18,2) NULL,
        [DefaultUsefulLifeYears] int NULL,
        [DefaultMaintenanceIntervalDays] int NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_AssetCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AssetCategories_AssetCategories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [AssetCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [AuditLogs] (
        [Id] nvarchar(450) NOT NULL,
        [UserId] nvarchar(450) NULL,
        [UserName] nvarchar(max) NULL,
        [Action] int NOT NULL,
        [EntityType] nvarchar(450) NOT NULL,
        [EntityId] nvarchar(450) NOT NULL,
        [EntityName] nvarchar(max) NULL,
        [OldValues] nvarchar(max) NULL,
        [NewValues] nvarchar(max) NULL,
        [ChangedProperties] nvarchar(max) NULL,
        [IpAddress] nvarchar(max) NULL,
        [UserAgent] nvarchar(max) NULL,
        [Notes] nvarchar(max) NULL,
        [Timestamp] datetime2 NOT NULL,
        CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ISOStandards] (
        [Id] nvarchar(450) NOT NULL,
        [StandardNumber] nvarchar(max) NOT NULL,
        [Version] nvarchar(max) NOT NULL,
        [Domain] nvarchar(max) NOT NULL,
        [IsCompliant] bit NOT NULL,
        [CompliancePercentage] int NOT NULL,
        [LastAuditDate] datetime2 NULL,
        [NextAuditDate] datetime2 NULL,
        [Notes] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        CONSTRAINT [PK_ISOStandards] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [KPITrends] (
        [Id] nvarchar(450) NOT NULL,
        [Category] nvarchar(max) NOT NULL,
        [KPIName] nvarchar(max) NOT NULL,
        [Year] int NOT NULL,
        [Value] decimal(18,2) NOT NULL,
        [TargetValue] decimal(18,2) NULL,
        [Unit] nvarchar(max) NOT NULL,
        [Notes] nvarchar(max) NULL,
        CONSTRAINT [PK_KPITrends] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [organization_units] (
        [unit_id] int NOT NULL IDENTITY,
        [unit_name] nvarchar(150) NOT NULL,
        [unit_name_ar] nvarchar(150) NOT NULL,
        [unit_type] nvarchar(50) NOT NULL,
        [parent_unit] int NULL,
        [created_by] int NOT NULL,
        [created_date] datetime2 NOT NULL,
        [update_by] int NOT NULL,
        [update_date] datetime2 NOT NULL,
        CONSTRAINT [PK_organization_units] PRIMARY KEY ([unit_id]),
        CONSTRAINT [FK_organization_units_organization_units_parent_unit] FOREIGN KEY ([parent_unit]) REFERENCES [organization_units] ([unit_id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [OrganizationUnits] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [ParentId] nvarchar(450) NULL,
        [Level] int NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [HeadUserId] nvarchar(max) NULL,
        [Email] nvarchar(max) NULL,
        [Phone] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_OrganizationUnits] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationUnits_OrganizationUnits_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ProcessMaturityAssessments] (
        [Id] nvarchar(450) NOT NULL,
        [AssessmentYear] int NOT NULL,
        [StrategicAlignment] decimal(18,2) NOT NULL,
        [Governance] decimal(18,2) NOT NULL,
        [ProcessModels] decimal(18,2) NOT NULL,
        [ChangeManagement] decimal(18,2) NOT NULL,
        [ProcessPerformance] decimal(18,2) NOT NULL,
        [ProcessImprovement] decimal(18,2) NOT NULL,
        [ToolsAndTechnology] decimal(18,2) NOT NULL,
        [OverallScore] decimal(18,2) NOT NULL,
        [AssessmentDate] datetime2 NOT NULL,
        [Notes] nvarchar(max) NULL,
        CONSTRAINT [PK_ProcessMaturityAssessments] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [RiskCategories] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [ParentCategoryId] nvarchar(450) NULL,
        [DefaultReviewFrequencyDays] int NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_RiskCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskCategories_RiskCategories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [RiskCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [roles] (
        [role_id] int NOT NULL IDENTITY,
        [role_name] nvarchar(100) NOT NULL,
        [role_name_ar] nvarchar(100) NULL,
        [description] nvarchar(500) NULL,
        [created_by] int NOT NULL,
        [created_date] datetime2 NOT NULL,
        [update_by] int NOT NULL,
        [update_date] datetime2 NOT NULL,
        CONSTRAINT [PK_roles] PRIMARY KEY ([role_id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [user] (
        [user_id] int NOT NULL IDENTITY,
        [username] nvarchar(300) NOT NULL,
        [employee_number] nvarchar(50) NULL,
        [email_address] nvarchar(150) NULL,
        [full_name] nvarchar(200) NULL,
        [employee_name] nvarchar(150) NULL,
        [employee_name_ar] nvarchar(150) NULL,
        [job_name] nvarchar(150) NULL,
        [job_name_ar] nvarchar(150) NULL,
        [direct_org_name_en] nvarchar(150) NULL,
        [direct_org_name_ar] nvarchar(150) NULL,
        [sector_id] int NULL,
        [unit_id] int NULL,
        [section_id] int NULL,
        [department] nvarchar(150) NULL,
        [department_ar] nvarchar(150) NULL,
        [direct_manager] int NULL,
        [is_department_coordinator] bit NULL,
        [password] nvarchar(50) NULL,
        [points] int NULL,
        [innovator_level] int NULL,
        [has_idea_generator_badge] bit NULL,
        [has_innovator_badge] bit NULL,
        [has_visionary_badge] bit NULL,
        [has_milestone_achiever_badge] bit NULL,
        [has_impactful_contributor_badge] bit NULL,
        CONSTRAINT [PK_user] PRIMARY KEY ([user_id]),
        CONSTRAINT [FK_user_organization_units_unit_id] FOREIGN KEY ([unit_id]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL,
        CONSTRAINT [FK_user_user_direct_manager] FOREIGN KEY ([direct_manager]) REFERENCES [user] ([user_id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [Categories] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [Tags] nvarchar(max) NULL,
        [AggregatedDurationMinutes] decimal(18,2) NULL,
        [AggregatedCost] decimal(18,2) NULL,
        [HasAutomatedProcesses] bit NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Categories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Categories_OrganizationUnits_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [OrganizationUnits] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [FeedbackCategories] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [ParentCategoryId] nvarchar(450) NULL,
        [DefaultPriority] int NULL,
        [DefaultAssignedToUnitId] nvarchar(450) NULL,
        [ExpectedResponseTimeHours] int NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_FeedbackCategories] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_FeedbackCategories_FeedbackCategories_ParentCategoryId] FOREIGN KEY ([ParentCategoryId]) REFERENCES [FeedbackCategories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_FeedbackCategories_OrganizationUnits_DefaultAssignedToUnitId] FOREIGN KEY ([DefaultAssignedToUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [StrategicObjectives] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [ParentId] nvarchar(450) NULL,
        [Level] int NOT NULL,
        [TargetYear] int NULL,
        [TargetValue] decimal(18,2) NULL,
        [CurrentValue] decimal(18,2) NULL,
        [UnitOfMeasure] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [Tags] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_StrategicObjectives] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_StrategicObjectives_OrganizationUnits_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [OrganizationUnits] ([Id]),
        CONSTRAINT [FK_StrategicObjectives_StrategicObjectives_ParentId] FOREIGN KEY ([ParentId]) REFERENCES [StrategicObjectives] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [SystemDefinitions] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [Vendor] nvarchar(max) NULL,
        [SystemVersion] nvarchar(max) NULL,
        [Url] nvarchar(max) NULL,
        [SystemType] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [SupportContact] nvarchar(max) NULL,
        [LicenseExpiryDate] datetime2 NULL,
        [AnnualLicenseCost] decimal(18,2) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_SystemDefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SystemDefinitions_OrganizationUnits_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [OrganizationUnits] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [user_roles] (
        [user_role_id] int NOT NULL IDENTITY,
        [user_id] int NOT NULL,
        [role_id] int NOT NULL,
        [assigned_by] int NOT NULL,
        [assigned_date] datetime2 NOT NULL,
        CONSTRAINT [PK_user_roles] PRIMARY KEY ([user_role_id]),
        CONSTRAINT [FK_user_roles_roles_role_id] FOREIGN KEY ([role_id]) REFERENCES [roles] ([role_id]) ON DELETE CASCADE,
        CONSTRAINT [FK_user_roles_user_user_id] FOREIGN KEY ([user_id]) REFERENCES [user] ([user_id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ProcessGroups] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [CategoryId] nvarchar(450) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [Tags] nvarchar(max) NULL,
        [AggregatedDurationMinutes] decimal(18,2) NULL,
        [AggregatedCost] decimal(18,2) NULL,
        [HasAutomatedProcesses] bit NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ProcessGroups] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProcessGroups_Categories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Categories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProcessGroups_OrganizationUnits_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [OrganizationUnits] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [Services] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [ServiceType] int NOT NULL,
        [Channel] int NOT NULL,
        [DisplayOrder] int NOT NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [StrategicObjectiveId] nvarchar(450) NULL,
        [SLADays] int NULL,
        [TargetDeliveryDays] decimal(18,2) NULL,
        [ActualDeliveryDays] decimal(18,2) NULL,
        [ServiceFee] decimal(18,2) NULL,
        [IsActive] bit NOT NULL,
        [Tags] nvarchar(max) NULL,
        [CustomerSatisfactionScore] decimal(18,2) NULL,
        [AnnualTransactionCount] int NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [EstimatedDuration] decimal(18,2) NULL,
        [DurationUnit] int NULL,
        [EstimatedCost] decimal(18,2) NULL,
        [ActualDuration] decimal(18,2) NULL,
        [ActualDurationUnit] int NULL,
        [ActualCost] decimal(18,2) NULL,
        [IsAutomated] bit NOT NULL,
        CONSTRAINT [PK_Services] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Services_OrganizationUnits_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [OrganizationUnits] ([Id]),
        CONSTRAINT [FK_Services_StrategicObjectives_StrategicObjectiveId] FOREIGN KEY ([StrategicObjectiveId]) REFERENCES [StrategicObjectives] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [Processes] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [ProcessGroupId] nvarchar(450) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [ProcessType] int NOT NULL,
        [Status] int NOT NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [Tags] nvarchar(max) NULL,
        [BpmnDiagram] nvarchar(max) NULL,
        [BpmnFilePath] nvarchar(max) NULL,
        [StrategicObjectiveId] nvarchar(450) NULL,
        [ServiceId] nvarchar(450) NULL,
        [SystemId] nvarchar(450) NULL,
        [AggregatedDurationMinutes] decimal(18,2) NULL,
        [AggregatedCost] decimal(18,2) NULL,
        [HasDetailedBreakdown] bit NOT NULL,
        [AutomationStatus] int NOT NULL,
        [DigitalSystemName] nvarchar(max) NULL,
        [AutomabilityStatus] int NOT NULL,
        [ClassificationType] int NOT NULL,
        [CurrentProposedStatus] int NOT NULL,
        [LinkedServices] nvarchar(max) NULL,
        [ExternalPartners] nvarchar(max) NULL,
        [GovernmentPartners] nvarchar(max) NULL,
        [LinkedProjects] nvarchar(max) NULL,
        [MandateResponsibilities] nvarchar(max) NULL,
        [DocumentReference] nvarchar(max) NULL,
        [DocumentLanguage] nvarchar(max) NULL,
        [AutomationAssessmentScores] nvarchar(max) NULL,
        [Tasks] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [EstimatedDuration] decimal(18,2) NULL,
        [DurationUnit] int NULL,
        [EstimatedCost] decimal(18,2) NULL,
        [ActualDuration] decimal(18,2) NULL,
        [ActualDurationUnit] int NULL,
        [ActualCost] decimal(18,2) NULL,
        [IsAutomated] bit NOT NULL,
        CONSTRAINT [PK_Processes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Processes_OrganizationUnits_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [OrganizationUnits] ([Id]),
        CONSTRAINT [FK_Processes_ProcessGroups_ProcessGroupId] FOREIGN KEY ([ProcessGroupId]) REFERENCES [ProcessGroups] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Processes_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Processes_StrategicObjectives_StrategicObjectiveId] FOREIGN KEY ([StrategicObjectiveId]) REFERENCES [StrategicObjectives] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Processes_SystemDefinitions_SystemId] FOREIGN KEY ([SystemId]) REFERENCES [SystemDefinitions] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ServiceAssessments] (
        [Id] nvarchar(450) NOT NULL,
        [ServiceId] nvarchar(450) NOT NULL,
        [Period] nvarchar(max) NOT NULL,
        [Automation] int NOT NULL,
        [SelfService] int NOT NULL,
        [DataIntegration] int NOT NULL,
        [Proactivity] int NOT NULL,
        [IntegratedServices] int NOT NULL,
        [NoPhysicalAttendance] int NOT NULL,
        [UnifiedChannels] int NOT NULL,
        [AssessmentDate] datetime2 NOT NULL,
        [Notes] nvarchar(max) NULL,
        CONSTRAINT [PK_ServiceAssessments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ServiceAssessments_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ServiceMeasurements] (
        [Id] nvarchar(450) NOT NULL,
        [ServiceId] nvarchar(450) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [Code] nvarchar(max) NOT NULL,
        [UnitOfMeasure] nvarchar(max) NULL,
        [TargetValue] decimal(18,2) NULL,
        [ActualValue] decimal(18,2) NULL,
        [MinValue] decimal(18,2) NULL,
        [MaxValue] decimal(18,2) NULL,
        [Frequency] nvarchar(max) NULL,
        [DataSource] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ServiceMeasurements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ServiceMeasurements_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [SLADefinitions] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [ServiceId] nvarchar(450) NULL,
        [MetricName] nvarchar(max) NOT NULL,
        [TargetValue] decimal(18,2) NOT NULL,
        [Unit] nvarchar(max) NOT NULL,
        [WarningThreshold] decimal(18,2) NULL,
        [MeasurementFrequency] nvarchar(max) NOT NULL,
        [CalculationMethod] nvarchar(max) NULL,
        [ResponsibleUnitId] nvarchar(450) NULL,
        [EffectiveFrom] datetime2 NOT NULL,
        [EffectiveTo] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [PenaltyForBreach] nvarchar(max) NULL,
        [EscalationProcedure] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_SLADefinitions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SLADefinitions_OrganizationUnits_ResponsibleUnitId] FOREIGN KEY ([ResponsibleUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_SLADefinitions_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [Activities] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [ProcessId] nvarchar(450) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [ChannelType] int NOT NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [Tags] nvarchar(max) NULL,
        [AggregatedDurationMinutes] decimal(18,2) NULL,
        [AggregatedCost] decimal(18,2) NULL,
        [HasDetailedBreakdown] bit NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [EstimatedDuration] decimal(18,2) NULL,
        [DurationUnit] int NULL,
        [EstimatedCost] decimal(18,2) NULL,
        [ActualDuration] decimal(18,2) NULL,
        [ActualDurationUnit] int NULL,
        [ActualCost] decimal(18,2) NULL,
        [IsAutomated] bit NOT NULL,
        CONSTRAINT [PK_Activities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Activities_OrganizationUnits_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [OrganizationUnits] ([Id]),
        CONSTRAINT [FK_Activities_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [Assets] (
        [Id] nvarchar(450) NOT NULL,
        [AssetTag] nvarchar(450) NOT NULL,
        [SerialNumber] nvarchar(max) NULL,
        [CategoryId] nvarchar(450) NOT NULL,
        [Manufacturer] nvarchar(max) NULL,
        [Model] nvarchar(max) NULL,
        [Status] int NOT NULL,
        [Location] nvarchar(max) NULL,
        [AssignedToUnitId] nvarchar(450) NULL,
        [AssignedToUserId] nvarchar(max) NULL,
        [ProcessId] nvarchar(450) NULL,
        [PurchaseDate] datetime2 NULL,
        [PurchaseCost] decimal(18,2) NULL,
        [CurrentValue] decimal(18,2) NULL,
        [DepreciationRate] decimal(18,2) NULL,
        [WarrantyExpiryDate] datetime2 NULL,
        [EndOfLifeDate] datetime2 NULL,
        [LastMaintenanceDate] datetime2 NULL,
        [NextMaintenanceDate] datetime2 NULL,
        [Criticality] int NOT NULL,
        [Notes] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Assets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Assets_AssetCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [AssetCategories] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_Assets_OrganizationUnits_AssignedToUnitId] FOREIGN KEY ([AssignedToUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Assets_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ChangeRequests] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [ProcessId] nvarchar(450) NULL,
        [ServiceId] nvarchar(450) NULL,
        [Status] int NOT NULL,
        [Source] int NOT NULL,
        [ExternalReferenceId] nvarchar(max) NULL,
        [Priority] int NOT NULL,
        [Justification] nvarchar(max) NULL,
        [ImpactAssessment] nvarchar(max) NULL,
        [RequestedById] nvarchar(max) NULL,
        [ApprovedById] nvarchar(max) NULL,
        [ApprovalDate] datetime2 NULL,
        [ImplementationDate] datetime2 NULL,
        [RejectionReason] nvarchar(max) NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ChangeRequests] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChangeRequests_OrganizationUnits_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [OrganizationUnits] ([Id]),
        CONSTRAINT [FK_ChangeRequests_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ChangeRequests_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [CustomerFeedbacks] (
        [Id] nvarchar(450) NOT NULL,
        [FeedbackNumber] nvarchar(450) NOT NULL,
        [Type] int NOT NULL,
        [CategoryId] nvarchar(450) NULL,
        [ServiceId] nvarchar(450) NULL,
        [ProcessId] nvarchar(450) NULL,
        [CustomerName] nvarchar(max) NOT NULL,
        [CustomerEmail] nvarchar(max) NULL,
        [CustomerPhone] nvarchar(max) NULL,
        [OrganizationName] nvarchar(max) NULL,
        [SubmittedDate] datetime2 NOT NULL,
        [Status] int NOT NULL,
        [Priority] int NOT NULL,
        [AssignedToId] nvarchar(max) NULL,
        [AssignedToUnitId] nvarchar(450) NULL,
        [Response] nvarchar(max) NULL,
        [ResponseDate] datetime2 NULL,
        [ResolutionDate] datetime2 NULL,
        [ResolutionNotes] nvarchar(max) NULL,
        [SatisfactionRating] int NULL,
        [RequiresFollowUp] bit NOT NULL,
        [FollowUpDate] datetime2 NULL,
        [RootCause] nvarchar(max) NULL,
        [CorrectiveAction] nvarchar(max) NULL,
        [PreventiveAction] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_CustomerFeedbacks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_CustomerFeedbacks_FeedbackCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [FeedbackCategories] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_CustomerFeedbacks_OrganizationUnits_AssignedToUnitId] FOREIGN KEY ([AssignedToUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_CustomerFeedbacks_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_CustomerFeedbacks_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [EnterpriseRisks] (
        [Id] nvarchar(450) NOT NULL,
        [RiskNumber] nvarchar(450) NOT NULL,
        [CategoryId] nvarchar(450) NOT NULL,
        [ProcessId] nvarchar(450) NULL,
        [OrganizationUnitId] nvarchar(450) NULL,
        [OwnerId] nvarchar(max) NULL,
        [Likelihood] int NOT NULL,
        [Impact] int NOT NULL,
        [InherentRiskScore] int NOT NULL,
        [ResidualLikelihood] int NULL,
        [ResidualImpact] int NULL,
        [ResidualRiskScore] int NULL,
        [RiskLevel] int NOT NULL,
        [ToleranceLevel] int NULL,
        [CurrentControls] nvarchar(max) NULL,
        [ControlEffectiveness] int NULL,
        [ResponseStrategy] nvarchar(max) NULL,
        [LastReviewDate] datetime2 NULL,
        [NextReviewDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_EnterpriseRisks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_EnterpriseRisks_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_EnterpriseRisks_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_EnterpriseRisks_RiskCategories_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [RiskCategories] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ImprovementInitiatives] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [TitleEn] nvarchar(max) NOT NULL,
        [TitleAr] nvarchar(max) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [ProcessId] nvarchar(450) NULL,
        [ServiceId] nvarchar(450) NULL,
        [ImpactScore] int NOT NULL,
        [EffortScore] int NOT NULL,
        [ProgressPercentage] int NOT NULL,
        [Quadrant] int NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Priority] int NOT NULL,
        [EstimatedCostSavings] decimal(18,2) NULL,
        [EstimatedTimeSavings] decimal(18,2) NULL,
        [ActualCostSavings] decimal(18,2) NULL,
        [ActualTimeSavings] decimal(18,2) NULL,
        [TargetDate] datetime2 NULL,
        [CompletedDate] datetime2 NULL,
        [OwnerId] nvarchar(max) NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [Source] int NOT NULL,
        [ExternalReferenceId] nvarchar(max) NULL,
        [InnovationType] int NULL,
        [Horizon] int NULL,
        [EaseOfImplementation] int NULL,
        [BudgetEstimation] int NULL,
        [ProjectDependency] int NULL,
        [StrategicAlignmentScore] int NULL,
        [LeadershipDirections] int NULL,
        [QualityOfLife] int NULL,
        [InnovationAndFutureShaping] int NULL,
        [FinancialAndEconomicImpact] int NULL,
        [SustainabilityScore] int NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ImprovementInitiatives] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImprovementInitiatives_OrganizationUnits_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [OrganizationUnits] ([Id]),
        CONSTRAINT [FK_ImprovementInitiatives_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ImprovementInitiatives_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ProcessBpmnVersions] (
        [Id] nvarchar(450) NOT NULL,
        [ProcessId] nvarchar(450) NOT NULL,
        [VersionNumber] int NOT NULL,
        [BpmnXml] nvarchar(max) NOT NULL,
        [ChangeDescription] nvarchar(500) NULL,
        [CreatedById] nvarchar(max) NULL,
        [CreatedByName] nvarchar(256) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsCurrent] bit NOT NULL,
        [XmlSizeBytes] int NOT NULL,
        CONSTRAINT [PK_ProcessBpmnVersions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProcessBpmnVersions_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ProcessMeasurements] (
        [Id] nvarchar(450) NOT NULL,
        [ProcessId] nvarchar(450) NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [Code] nvarchar(max) NOT NULL,
        [UnitOfMeasure] nvarchar(max) NULL,
        [TargetValue] decimal(18,2) NULL,
        [ActualValue] decimal(18,2) NULL,
        [MinValue] decimal(18,2) NULL,
        [MaxValue] decimal(18,2) NULL,
        [Frequency] nvarchar(max) NULL,
        [DataSource] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ProcessMeasurements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProcessMeasurements_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ProcessRacis] (
        [Id] nvarchar(450) NOT NULL,
        [ProcessId] nvarchar(450) NOT NULL,
        [OrganizationUnitId] nvarchar(450) NOT NULL,
        [Role] int NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ProcessRacis] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProcessRacis_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProcessRacis_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ActivityRacis] (
        [Id] nvarchar(450) NOT NULL,
        [ActivityId] nvarchar(450) NOT NULL,
        [OrganizationUnitId] nvarchar(450) NOT NULL,
        [Role] int NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ActivityRacis] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ActivityRacis_Activities_ActivityId] FOREIGN KEY ([ActivityId]) REFERENCES [Activities] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ActivityRacis_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ProcessTasks] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [ActivityId] nvarchar(450) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [ChannelType] int NOT NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [Tags] nvarchar(max) NULL,
        [SystemId] nvarchar(450) NULL,
        [ProcedureStatus] int NOT NULL,
        [AutomationStatus] int NOT NULL,
        [DigitalSystemName] nvarchar(max) NULL,
        [AutomabilityStatus] int NOT NULL,
        [CurrentProposedStatus] int NOT NULL,
        [AutomationAssessmentScores] nvarchar(max) NULL,
        [LinkedServices] nvarchar(max) NULL,
        [DocumentReference] nvarchar(max) NULL,
        [DocumentLanguage] nvarchar(max) NULL,
        [BpmnDiagram] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        [EstimatedDuration] decimal(18,2) NULL,
        [DurationUnit] int NULL,
        [EstimatedCost] decimal(18,2) NULL,
        [ActualDuration] decimal(18,2) NULL,
        [ActualDurationUnit] int NULL,
        [ActualCost] decimal(18,2) NULL,
        [IsAutomated] bit NOT NULL,
        CONSTRAINT [PK_ProcessTasks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProcessTasks_Activities_ActivityId] FOREIGN KEY ([ActivityId]) REFERENCES [Activities] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ProcessTasks_OrganizationUnits_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [OrganizationUnits] ([Id]),
        CONSTRAINT [FK_ProcessTasks_SystemDefinitions_SystemId] FOREIGN KEY ([SystemId]) REFERENCES [SystemDefinitions] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [MaintenanceSchedules] (
        [Id] nvarchar(450) NOT NULL,
        [AssetId] nvarchar(450) NOT NULL,
        [Type] int NOT NULL,
        [FrequencyDays] int NOT NULL,
        [LastPerformedDate] datetime2 NULL,
        [NextScheduledDate] datetime2 NOT NULL,
        [EstimatedDurationHours] decimal(18,2) NULL,
        [EstimatedCost] decimal(18,2) NULL,
        [AssignedToId] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [Instructions] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_MaintenanceSchedules] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaintenanceSchedules_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [Problems] (
        [Id] nvarchar(450) NOT NULL,
        [ProblemNumber] nvarchar(450) NOT NULL,
        [Priority] int NOT NULL,
        [Impact] int NOT NULL,
        [Status] int NOT NULL,
        [Category] nvarchar(max) NOT NULL,
        [Subcategory] nvarchar(max) NULL,
        [ServiceId] nvarchar(450) NULL,
        [ProcessId] nvarchar(450) NULL,
        [AssetId] nvarchar(450) NULL,
        [OwnerId] nvarchar(max) NULL,
        [AssignedToUnitId] nvarchar(450) NULL,
        [IdentifiedAt] datetime2 NOT NULL,
        [RootCauseIdentifiedAt] datetime2 NULL,
        [ResolvedAt] datetime2 NULL,
        [ClosedAt] datetime2 NULL,
        [RootCauseAnalysis] nvarchar(max) NULL,
        [Workaround] nvarchar(max) NULL,
        [PermanentSolution] nvarchar(max) NULL,
        [IsKnownError] bit NOT NULL,
        [KnowledgeBaseArticleId] nvarchar(max) NULL,
        [RelatedIncidentCount] int NOT NULL,
        [EstimatedCostImpact] decimal(18,2) NULL,
        [ActualCostImpact] decimal(18,2) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Problems] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Problems_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Problems_OrganizationUnits_AssignedToUnitId] FOREIGN KEY ([AssignedToUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Problems_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Problems_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ServiceAssets] (
        [ServiceId] nvarchar(450) NOT NULL,
        [AssetId] nvarchar(450) NOT NULL,
        [Id] nvarchar(max) NULL,
        [Criticality] int NOT NULL,
        [IsRequired] bit NOT NULL,
        [UsageDescription] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        CONSTRAINT [PK_ServiceAssets] PRIMARY KEY ([ServiceId], [AssetId]),
        CONSTRAINT [FK_ServiceAssets_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ServiceAssets_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ChangeRequestAssets] (
        [ChangeRequestId] nvarchar(450) NOT NULL,
        [AssetId] nvarchar(450) NOT NULL,
        [Id] nvarchar(max) NULL,
        [ImpactType] nvarchar(max) NOT NULL,
        [ImpactDescription] nvarchar(max) NULL,
        [IsCritical] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        CONSTRAINT [PK_ChangeRequestAssets] PRIMARY KEY ([ChangeRequestId], [AssetId]),
        CONSTRAINT [FK_ChangeRequestAssets_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChangeRequestAssets_ChangeRequests_ChangeRequestId] FOREIGN KEY ([ChangeRequestId]) REFERENCES [ChangeRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ChangeRequestComments] (
        [Id] nvarchar(450) NOT NULL,
        [ChangeRequestId] nvarchar(450) NOT NULL,
        [Comment] nvarchar(max) NOT NULL,
        [UserId] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ChangeRequestComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ChangeRequestComments_ChangeRequests_ChangeRequestId] FOREIGN KEY ([ChangeRequestId]) REFERENCES [ChangeRequests] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [AssetRisks] (
        [AssetId] nvarchar(450) NOT NULL,
        [RiskId] nvarchar(450) NOT NULL,
        [Id] nvarchar(max) NULL,
        [ImpactLevel] int NOT NULL,
        [SpecificControls] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        CONSTRAINT [PK_AssetRisks] PRIMARY KEY ([AssetId], [RiskId]),
        CONSTRAINT [FK_AssetRisks_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_AssetRisks_EnterpriseRisks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [EnterpriseRisks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ChangeRequestRisks] (
        [ChangeRequestId] nvarchar(450) NOT NULL,
        [RiskId] nvarchar(450) NOT NULL,
        [Id] nvarchar(max) NULL,
        [RelationshipType] nvarchar(max) NOT NULL,
        [ImpactDescription] nvarchar(max) NULL,
        [ExpectedRiskChange] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        CONSTRAINT [PK_ChangeRequestRisks] PRIMARY KEY ([ChangeRequestId], [RiskId]),
        CONSTRAINT [FK_ChangeRequestRisks_ChangeRequests_ChangeRequestId] FOREIGN KEY ([ChangeRequestId]) REFERENCES [ChangeRequests] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ChangeRequestRisks_EnterpriseRisks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [EnterpriseRisks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ProcessRisks] (
        [Id] nvarchar(450) NOT NULL,
        [ProcessId] nvarchar(450) NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [Category] nvarchar(max) NULL,
        [EnterpriseRiskId] nvarchar(450) NULL,
        [LikelihoodScore] int NOT NULL,
        [ImpactScore] int NOT NULL,
        [MitigationStrategy] nvarchar(max) NULL,
        [OwnerId] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ProcessRisks] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProcessRisks_EnterpriseRisks_EnterpriseRiskId] FOREIGN KEY ([EnterpriseRiskId]) REFERENCES [EnterpriseRisks] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_ProcessRisks_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [RiskActionPlans] (
        [Id] nvarchar(450) NOT NULL,
        [RiskId] nvarchar(450) NOT NULL,
        [OwnerId] nvarchar(max) NULL,
        [Priority] int NOT NULL,
        [TargetDate] datetime2 NULL,
        [CompletionDate] datetime2 NULL,
        [Status] nvarchar(max) NOT NULL,
        [ProgressPercentage] int NOT NULL,
        [EstimatedCost] decimal(18,2) NULL,
        [ActualCost] decimal(18,2) NULL,
        [ExpectedRiskReduction] int NULL,
        [Notes] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        CONSTRAINT [PK_RiskActionPlans] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_RiskActionPlans_EnterpriseRisks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [EnterpriseRisks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ServiceRisks] (
        [ServiceId] nvarchar(450) NOT NULL,
        [RiskId] nvarchar(450) NOT NULL,
        [Id] nvarchar(max) NULL,
        [ImpactLevel] int NOT NULL,
        [SpecificControls] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        CONSTRAINT [PK_ServiceRisks] PRIMARY KEY ([ServiceId], [RiskId]),
        CONSTRAINT [FK_ServiceRisks_EnterpriseRisks_RiskId] FOREIGN KEY ([RiskId]) REFERENCES [EnterpriseRisks] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ServiceRisks_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ImprovementActions] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(max) NOT NULL,
        [ImprovementId] nvarchar(450) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Priority] int NOT NULL,
        [DueDate] datetime2 NULL,
        [CompletedDate] datetime2 NULL,
        [AssignedToId] nvarchar(max) NULL,
        [CompletionPercentage] int NOT NULL,
        [Notes] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ImprovementActions] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImprovementActions_ImprovementInitiatives_ImprovementId] FOREIGN KEY ([ImprovementId]) REFERENCES [ImprovementInitiatives] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ImprovementMeasurements] (
        [Id] nvarchar(450) NOT NULL,
        [ImprovementId] nvarchar(450) NOT NULL,
        [MeasurementType] int NOT NULL,
        [UnitOfMeasure] nvarchar(max) NOT NULL,
        [TargetValue] decimal(18,2) NULL,
        [AsIsValue] decimal(18,2) NULL,
        [ToBeValue] decimal(18,2) NULL,
        [Weight] int NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ImprovementMeasurements] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImprovementMeasurements_ImprovementInitiatives_ImprovementId] FOREIGN KEY ([ImprovementId]) REFERENCES [ImprovementInitiatives] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ImprovementTeamMembers] (
        [Id] nvarchar(450) NOT NULL,
        [ImprovementId] nvarchar(450) NOT NULL,
        [UserId] nvarchar(max) NOT NULL,
        [Role] int NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ImprovementTeamMembers] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImprovementTeamMembers_ImprovementInitiatives_ImprovementId] FOREIGN KEY ([ImprovementId]) REFERENCES [ImprovementInitiatives] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [TaskRacis] (
        [Id] nvarchar(450) NOT NULL,
        [TaskId] nvarchar(450) NOT NULL,
        [OrganizationUnitId] nvarchar(450) NOT NULL,
        [Role] int NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TaskRacis] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TaskRacis_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_TaskRacis_ProcessTasks_TaskId] FOREIGN KEY ([TaskId]) REFERENCES [ProcessTasks] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [MaintenanceRecords] (
        [Id] nvarchar(450) NOT NULL,
        [AssetId] nvarchar(450) NOT NULL,
        [MaintenanceScheduleId] nvarchar(450) NULL,
        [Type] int NOT NULL,
        [PerformedDate] datetime2 NOT NULL,
        [PerformedById] nvarchar(max) NULL,
        [VendorName] nvarchar(max) NULL,
        [DurationHours] decimal(18,2) NULL,
        [Cost] decimal(18,2) NULL,
        [WorkPerformed] nvarchar(max) NULL,
        [PartsReplaced] nvarchar(max) NULL,
        [IssuesFound] nvarchar(max) NULL,
        [Recommendations] nvarchar(max) NULL,
        [NextMaintenanceDue] datetime2 NULL,
        [DowntimeHours] decimal(18,2) NULL,
        [IsCompleted] bit NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        CONSTRAINT [PK_MaintenanceRecords] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MaintenanceRecords_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_MaintenanceRecords_MaintenanceSchedules_MaintenanceScheduleId] FOREIGN KEY ([MaintenanceScheduleId]) REFERENCES [MaintenanceSchedules] ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [Incidents] (
        [Id] nvarchar(450) NOT NULL,
        [IncidentNumber] nvarchar(450) NOT NULL,
        [Priority] int NOT NULL,
        [Impact] int NOT NULL,
        [Urgency] int NOT NULL,
        [Status] int NOT NULL,
        [Category] nvarchar(max) NOT NULL,
        [Subcategory] nvarchar(max) NULL,
        [ServiceId] nvarchar(450) NULL,
        [ProcessId] nvarchar(450) NULL,
        [AssetId] nvarchar(450) NULL,
        [ReportedById] nvarchar(max) NULL,
        [AssignedToId] nvarchar(max) NULL,
        [AssignedToUnitId] nvarchar(450) NULL,
        [ProblemId] nvarchar(450) NULL,
        [ReportedAt] datetime2 NOT NULL,
        [AcknowledgedAt] datetime2 NULL,
        [ResolvedAt] datetime2 NULL,
        [ClosedAt] datetime2 NULL,
        [SlaTargetHours] int NOT NULL,
        [SlaDueDate] datetime2 NOT NULL,
        [SlaBreached] bit NOT NULL,
        [ResolutionNotes] nvarchar(max) NULL,
        [Workaround] nvarchar(max) NULL,
        [RootCause] nvarchar(max) NULL,
        [SatisfactionRating] int NULL,
        [CustomerFeedback] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_Incidents] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Incidents_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Incidents_OrganizationUnits_AssignedToUnitId] FOREIGN KEY ([AssignedToUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Incidents_Problems_ProblemId] FOREIGN KEY ([ProblemId]) REFERENCES [Problems] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Incidents_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_Incidents_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [ProblemComments] (
        [Id] nvarchar(450) NOT NULL,
        [ProblemId] nvarchar(450) NOT NULL,
        [Comment] nvarchar(max) NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsInternal] bit NOT NULL,
        CONSTRAINT [PK_ProblemComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ProblemComments_Problems_ProblemId] FOREIGN KEY ([ProblemId]) REFERENCES [Problems] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [IncidentComments] (
        [Id] nvarchar(450) NOT NULL,
        [IncidentId] nvarchar(450) NOT NULL,
        [Comment] nvarchar(max) NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [IsInternal] bit NOT NULL,
        CONSTRAINT [PK_IncidentComments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_IncidentComments_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE TABLE [SLABreaches] (
        [Id] nvarchar(450) NOT NULL,
        [BreachNumber] nvarchar(450) NOT NULL,
        [SLADefinitionId] nvarchar(450) NOT NULL,
        [IncidentId] nvarchar(450) NULL,
        [BreachDate] datetime2 NOT NULL,
        [TargetValue] decimal(18,2) NOT NULL,
        [ActualValue] decimal(18,2) NOT NULL,
        [Variance] decimal(18,2) NOT NULL,
        [VariancePercentage] decimal(18,2) NOT NULL,
        [Severity] int NOT NULL,
        [RootCause] nvarchar(max) NULL,
        [CorrectiveAction] nvarchar(max) NULL,
        [PreventiveAction] nvarchar(max) NULL,
        [ResponsibleUserId] nvarchar(max) NULL,
        [AcknowledgedDate] datetime2 NULL,
        [ResolvedDate] datetime2 NULL,
        [IsResolved] bit NOT NULL,
        [FinancialImpact] decimal(18,2) NULL,
        [CustomerImpact] nvarchar(max) NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        CONSTRAINT [PK_SLABreaches] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_SLABreaches_Incidents_IncidentId] FOREIGN KEY ([IncidentId]) REFERENCES [Incidents] ([Id]) ON DELETE SET NULL,
        CONSTRAINT [FK_SLABreaches_SLADefinitions_SLADefinitionId] FOREIGN KEY ([SLADefinitionId]) REFERENCES [SLADefinitions] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Activities_Code] ON [Activities] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Activities_OwningUnitId] ON [Activities] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Activities_ProcessId] ON [Activities] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ActivityRacis_ActivityId] ON [ActivityRacis] ([ActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ActivityRacis_OrganizationUnitId] ON [ActivityRacis] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_AssetCategories_Code] ON [AssetCategories] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_AssetCategories_ParentCategoryId] ON [AssetCategories] ([ParentCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_AssetRisks_RiskId] ON [AssetRisks] ([RiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Assets_AssetTag] ON [Assets] ([AssetTag]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Assets_AssignedToUnitId] ON [Assets] ([AssignedToUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Assets_CategoryId] ON [Assets] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Assets_ProcessId] ON [Assets] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Assets_Status] ON [Assets] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityId] ON [AuditLogs] ([EntityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_EntityType] ON [AuditLogs] ([EntityType]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_Timestamp] ON [AuditLogs] ([Timestamp]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_AuditLogs_UserId] ON [AuditLogs] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Categories_Code] ON [Categories] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Categories_DisplayOrder] ON [Categories] ([DisplayOrder]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Categories_OwningUnitId] ON [Categories] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ChangeRequestAssets_AssetId] ON [ChangeRequestAssets] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ChangeRequestComments_ChangeRequestId] ON [ChangeRequestComments] ([ChangeRequestId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ChangeRequestRisks_RiskId] ON [ChangeRequestRisks] ([RiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ChangeRequests_OwningUnitId] ON [ChangeRequests] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ChangeRequests_ProcessId] ON [ChangeRequests] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ChangeRequests_ServiceId] ON [ChangeRequests] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_CustomerFeedbacks_AssignedToUnitId] ON [CustomerFeedbacks] ([AssignedToUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_CustomerFeedbacks_CategoryId] ON [CustomerFeedbacks] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_CustomerFeedbacks_FeedbackNumber] ON [CustomerFeedbacks] ([FeedbackNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_CustomerFeedbacks_ProcessId] ON [CustomerFeedbacks] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_CustomerFeedbacks_ServiceId] ON [CustomerFeedbacks] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_CustomerFeedbacks_Status] ON [CustomerFeedbacks] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_EnterpriseRisks_CategoryId] ON [EnterpriseRisks] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_EnterpriseRisks_OrganizationUnitId] ON [EnterpriseRisks] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_EnterpriseRisks_ProcessId] ON [EnterpriseRisks] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_EnterpriseRisks_RiskLevel] ON [EnterpriseRisks] ([RiskLevel]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_EnterpriseRisks_RiskNumber] ON [EnterpriseRisks] ([RiskNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_FeedbackCategories_Code] ON [FeedbackCategories] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_FeedbackCategories_DefaultAssignedToUnitId] ON [FeedbackCategories] ([DefaultAssignedToUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_FeedbackCategories_ParentCategoryId] ON [FeedbackCategories] ([ParentCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ImprovementActions_ImprovementId] ON [ImprovementActions] ([ImprovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ImprovementInitiatives_OwningUnitId] ON [ImprovementInitiatives] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ImprovementInitiatives_ProcessId] ON [ImprovementInitiatives] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ImprovementInitiatives_ServiceId] ON [ImprovementInitiatives] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ImprovementMeasurements_ImprovementId] ON [ImprovementMeasurements] ([ImprovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ImprovementTeamMembers_ImprovementId] ON [ImprovementTeamMembers] ([ImprovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_IncidentComments_IncidentId] ON [IncidentComments] ([IncidentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Incidents_AssetId] ON [Incidents] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Incidents_AssignedToUnitId] ON [Incidents] ([AssignedToUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Incidents_IncidentNumber] ON [Incidents] ([IncidentNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Incidents_Priority] ON [Incidents] ([Priority]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Incidents_ProblemId] ON [Incidents] ([ProblemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Incidents_ProcessId] ON [Incidents] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Incidents_ReportedAt] ON [Incidents] ([ReportedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Incidents_ServiceId] ON [Incidents] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Incidents_Status] ON [Incidents] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_MaintenanceRecords_AssetId] ON [MaintenanceRecords] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_MaintenanceRecords_MaintenanceScheduleId] ON [MaintenanceRecords] ([MaintenanceScheduleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_MaintenanceSchedules_AssetId] ON [MaintenanceSchedules] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_organization_units_parent_unit] ON [organization_units] ([parent_unit]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_OrganizationUnits_Code] ON [OrganizationUnits] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_OrganizationUnits_ParentId] ON [OrganizationUnits] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProblemComments_ProblemId] ON [ProblemComments] ([ProblemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Problems_AssetId] ON [Problems] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Problems_AssignedToUnitId] ON [Problems] ([AssignedToUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Problems_ProblemNumber] ON [Problems] ([ProblemNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Problems_ProcessId] ON [Problems] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Problems_ServiceId] ON [Problems] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Problems_Status] ON [Problems] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessBpmnVersions_CreatedAt] ON [ProcessBpmnVersions] ([CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessBpmnVersions_ProcessId] ON [ProcessBpmnVersions] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProcessBpmnVersions_ProcessId_VersionNumber] ON [ProcessBpmnVersions] ([ProcessId], [VersionNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Processes_Code] ON [Processes] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Processes_OwningUnitId] ON [Processes] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Processes_ProcessGroupId] ON [Processes] ([ProcessGroupId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Processes_ServiceId] ON [Processes] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Processes_Status] ON [Processes] ([Status]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Processes_StrategicObjectiveId] ON [Processes] ([StrategicObjectiveId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Processes_SystemId] ON [Processes] ([SystemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessGroups_CategoryId] ON [ProcessGroups] ([CategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProcessGroups_Code] ON [ProcessGroups] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessGroups_OwningUnitId] ON [ProcessGroups] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessMeasurements_ProcessId] ON [ProcessMeasurements] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessRacis_OrganizationUnitId] ON [ProcessRacis] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessRacis_ProcessId] ON [ProcessRacis] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessRisks_EnterpriseRiskId] ON [ProcessRisks] ([EnterpriseRiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessRisks_ProcessId] ON [ProcessRisks] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessTasks_ActivityId] ON [ProcessTasks] ([ActivityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ProcessTasks_Code] ON [ProcessTasks] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessTasks_OwningUnitId] ON [ProcessTasks] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ProcessTasks_SystemId] ON [ProcessTasks] ([SystemId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_RiskActionPlans_RiskId] ON [RiskActionPlans] ([RiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_RiskCategories_Code] ON [RiskCategories] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_RiskCategories_ParentCategoryId] ON [RiskCategories] ([ParentCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_roles_role_name] ON [roles] ([role_name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ServiceAssessments_ServiceId] ON [ServiceAssessments] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ServiceAssets_AssetId] ON [ServiceAssets] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ServiceMeasurements_ServiceId] ON [ServiceMeasurements] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_ServiceRisks_RiskId] ON [ServiceRisks] ([RiskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Services_Code] ON [Services] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Services_OwningUnitId] ON [Services] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_Services_StrategicObjectiveId] ON [Services] ([StrategicObjectiveId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_SLABreaches_BreachDate] ON [SLABreaches] ([BreachDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SLABreaches_BreachNumber] ON [SLABreaches] ([BreachNumber]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_SLABreaches_IncidentId] ON [SLABreaches] ([IncidentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_SLABreaches_SLADefinitionId] ON [SLABreaches] ([SLADefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_SLADefinitions_Code] ON [SLADefinitions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_SLADefinitions_ResponsibleUnitId] ON [SLADefinitions] ([ResponsibleUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_SLADefinitions_ServiceId] ON [SLADefinitions] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_StrategicObjectives_Code] ON [StrategicObjectives] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_StrategicObjectives_OwningUnitId] ON [StrategicObjectives] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_StrategicObjectives_ParentId] ON [StrategicObjectives] ([ParentId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_SystemDefinitions_OwningUnitId] ON [SystemDefinitions] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_TaskRacis_OrganizationUnitId] ON [TaskRacis] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_TaskRacis_TaskId] ON [TaskRacis] ([TaskId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_user_direct_manager] ON [user] ([direct_manager]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_user_unit_id] ON [user] ([unit_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_user_username] ON [user] ([username]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE INDEX [IX_user_roles_role_id] ON [user_roles] ([role_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    CREATE UNIQUE INDEX [IX_user_roles_user_id_role_id] ON [user_roles] ([user_id], [role_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260214095417_InitialCustomUserTables'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260214095417_InitialCustomUserTables', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221001129_AddImprovementProcessServiceLinks'
)
BEGIN
    CREATE TABLE [ImprovementProcesses] (
        [ImprovementId] nvarchar(450) NOT NULL,
        [ProcessId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        CONSTRAINT [PK_ImprovementProcesses] PRIMARY KEY ([ImprovementId], [ProcessId]),
        CONSTRAINT [FK_ImprovementProcesses_ImprovementInitiatives_ImprovementId] FOREIGN KEY ([ImprovementId]) REFERENCES [ImprovementInitiatives] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ImprovementProcesses_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221001129_AddImprovementProcessServiceLinks'
)
BEGIN
    CREATE TABLE [ImprovementServices] (
        [ImprovementId] nvarchar(450) NOT NULL,
        [ServiceId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        CONSTRAINT [PK_ImprovementServices] PRIMARY KEY ([ImprovementId], [ServiceId]),
        CONSTRAINT [FK_ImprovementServices_ImprovementInitiatives_ImprovementId] FOREIGN KEY ([ImprovementId]) REFERENCES [ImprovementInitiatives] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ImprovementServices_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221001129_AddImprovementProcessServiceLinks'
)
BEGIN
    CREATE INDEX [IX_ImprovementProcesses_ProcessId] ON [ImprovementProcesses] ([ProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221001129_AddImprovementProcessServiceLinks'
)
BEGIN
    CREATE INDEX [IX_ImprovementServices_ServiceId] ON [ImprovementServices] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221001129_AddImprovementProcessServiceLinks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260221001129_AddImprovementProcessServiceLinks', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221090356_AddMeasurementFieldsPeriodMethodBpmn'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD [AppliesTo] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221090356_AddMeasurementFieldsPeriodMethodBpmn'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD [BpmnReference] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221090356_AddMeasurementFieldsPeriodMethodBpmn'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD [MeasuringMethod] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221090356_AddMeasurementFieldsPeriodMethodBpmn'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD [MeasuringPeriod] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221090356_AddMeasurementFieldsPeriodMethodBpmn'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260221090356_AddMeasurementFieldsPeriodMethodBpmn', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221112508_AddMeasurementPriority'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD [Priority] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260221112508_AddMeasurementPriority'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260221112508_AddMeasurementPriority', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260417154726_BaselineDrift'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260417154726_BaselineDrift', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418090127_ApprovalSlaAndDelegation'
)
BEGIN
    ALTER TABLE [WorkflowSteps] ADD [DelegatedFromName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418090127_ApprovalSlaAndDelegation'
)
BEGIN
    ALTER TABLE [WorkflowSteps] ADD [DelegatedFromUserId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418090127_ApprovalSlaAndDelegation'
)
BEGIN
    ALTER TABLE [WorkflowSteps] ADD [DelegationExpiresAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418090127_ApprovalSlaAndDelegation'
)
BEGIN
    ALTER TABLE [WorkflowSteps] ADD [DueAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418090127_ApprovalSlaAndDelegation'
)
BEGIN
    ALTER TABLE [WorkflowSteps] ADD [EscalatedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418090127_ApprovalSlaAndDelegation'
)
BEGIN
    ALTER TABLE [ApprovalConfigurations] ADD [EscalationUserId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418090127_ApprovalSlaAndDelegation'
)
BEGIN
    ALTER TABLE [ApprovalConfigurations] ADD [EscalationUserName] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418090127_ApprovalSlaAndDelegation'
)
BEGIN
    ALTER TABLE [ApprovalConfigurations] ADD [Level1SlaHours] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418090127_ApprovalSlaAndDelegation'
)
BEGIN
    ALTER TABLE [ApprovalConfigurations] ADD [Level2SlaHours] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260418090127_ApprovalSlaAndDelegation'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260418090127_ApprovalSlaAndDelegation', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    IF COL_LENGTH('ProcessGroups','LegacyCode') IS NULL ALTER TABLE [ProcessGroups] ADD [LegacyCode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    IF COL_LENGTH('ProcessGroups','SortKey') IS NULL ALTER TABLE [ProcessGroups] ADD [SortKey] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    IF COL_LENGTH('Processes','LegacyCode') IS NULL ALTER TABLE [Processes] ADD [LegacyCode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    IF COL_LENGTH('Processes','ParentProcessId') IS NULL ALTER TABLE [Processes] ADD [ParentProcessId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    IF COL_LENGTH('Processes','SortKey') IS NULL ALTER TABLE [Processes] ADD [SortKey] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    IF COL_LENGTH('Categories','LegacyCode') IS NULL ALTER TABLE [Categories] ADD [LegacyCode] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    IF COL_LENGTH('Categories','SortKey') IS NULL ALTER TABLE [Categories] ADD [SortKey] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ImprovementTeamMembers]') AND [c].[name] = N'UserId');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [ImprovementTeamMembers] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [ImprovementTeamMembers] ALTER COLUMN [UserId] int NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    DECLARE @var1 sysname;
    SELECT @var1 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ImprovementMeasurements]') AND [c].[name] = N'Direction');
    IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [ImprovementMeasurements] DROP CONSTRAINT [' + @var1 + '];');
    ALTER TABLE [ImprovementMeasurements] ALTER COLUMN [Direction] nvarchar(20) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD [AppliesToProcessId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD [AppliesToServiceId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN

                    UPDATE ImprovementMeasurements
                    SET AppliesToProcessId = SUBSTRING(AppliesTo, 9, 4000)
                    WHERE AppliesTo LIKE 'process:%';

                    UPDATE ImprovementMeasurements
                    SET AppliesToServiceId = SUBSTRING(AppliesTo, 9, 4000)
                    WHERE AppliesTo LIKE 'service:%';
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    DECLARE @var2 sysname;
    SELECT @var2 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ImprovementMeasurements]') AND [c].[name] = N'AppliesTo');
    IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [ImprovementMeasurements] DROP CONSTRAINT [' + @var2 + '];');
    ALTER TABLE [ImprovementMeasurements] DROP COLUMN [AppliesTo];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN

                    IF EXISTS (SELECT 1 FROM sys.indexes
                               WHERE name = 'IX_ImprovementInitiatives_Status'
                                 AND object_id = OBJECT_ID('ImprovementInitiatives'))
                        DROP INDEX [IX_ImprovementInitiatives_Status] ON [ImprovementInitiatives];

                    ALTER TABLE [ImprovementInitiatives] ALTER COLUMN [Status] nvarchar(50) NOT NULL;

                    CREATE INDEX [IX_ImprovementInitiatives_Status]
                        ON [ImprovementInitiatives]([Status]);

                    ALTER TABLE [ImprovementActions] ALTER COLUMN [Status] nvarchar(50) NOT NULL;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN

                    UPDATE ImprovementInitiatives SET Status = 'Proposed'  WHERE Status = 'Identified';
                    UPDATE ImprovementInitiatives SET Status = 'Completed' WHERE Status = 'Implemented';
                    UPDATE ImprovementActions     SET Status = 'InProgress' WHERE Status = 'In Progress';
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    CREATE TABLE [PrioritizationConfigs] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [FiscalYear] int NULL,
        [ImpactCutoff] int NOT NULL,
        [EffortCutoff] int NOT NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PrioritizationConfigs] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    CREATE INDEX [IX_ImprovementTeamMembers_UserId] ON [ImprovementTeamMembers] ([UserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    CREATE INDEX [IX_ImprovementMeasurements_AppliesToProcessId] ON [ImprovementMeasurements] ([AppliesToProcessId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    CREATE INDEX [IX_ImprovementMeasurements_AppliesToServiceId] ON [ImprovementMeasurements] ([AppliesToServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD CONSTRAINT [FK_ImprovementMeasurements_Processes_AppliesToProcessId] FOREIGN KEY ([AppliesToProcessId]) REFERENCES [Processes] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD CONSTRAINT [FK_ImprovementMeasurements_Services_AppliesToServiceId] FOREIGN KEY ([AppliesToServiceId]) REFERENCES [Services] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    ALTER TABLE [ImprovementTeamMembers] ADD CONSTRAINT [FK_ImprovementTeamMembers_user_UserId] FOREIGN KEY ([UserId]) REFERENCES [user] ([user_id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503170817_AuditBatchA_QuickWins'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260503170817_AuditBatchA_QuickWins', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD [IsBenefitTracked] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    ALTER TABLE [ImprovementInitiatives] ADD [StrategicObjectiveId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE TABLE [ImprovementAssets] (
        [Id] nvarchar(450) NOT NULL,
        [ImprovementId] nvarchar(450) NOT NULL,
        [AssetId] nvarchar(450) NOT NULL,
        [RelationshipType] nvarchar(20) NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        CONSTRAINT [PK_ImprovementAssets] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImprovementAssets_Assets_AssetId] FOREIGN KEY ([AssetId]) REFERENCES [Assets] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ImprovementAssets_ImprovementInitiatives_ImprovementId] FOREIGN KEY ([ImprovementId]) REFERENCES [ImprovementInitiatives] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE TABLE [ImprovementBenefitsReviews] (
        [Id] nvarchar(450) NOT NULL,
        [ImprovementId] nvarchar(450) NOT NULL,
        [Period] nvarchar(20) NOT NULL,
        [DueDate] datetime2 NOT NULL,
        [Outcome] nvarchar(30) NOT NULL,
        [ActualCostSaving] decimal(18,2) NULL,
        [ActualTimeSaving] decimal(18,2) NULL,
        [Notes] nvarchar(2000) NULL,
        [ReviewedById] nvarchar(150) NULL,
        [ReviewedByName] nvarchar(200) NULL,
        [ReviewedAt] datetime2 NULL,
        [SignedOffById] nvarchar(150) NULL,
        [SignedOffByName] nvarchar(200) NULL,
        [SignedOffAt] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ImprovementBenefitsReviews] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImprovementBenefitsReviews_ImprovementInitiatives_ImprovementId] FOREIGN KEY ([ImprovementId]) REFERENCES [ImprovementInitiatives] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE TABLE [ImprovementChangeLogs] (
        [Id] nvarchar(450) NOT NULL,
        [ImprovementId] nvarchar(450) NOT NULL,
        [FieldName] nvarchar(100) NOT NULL,
        [OldValue] nvarchar(2000) NULL,
        [NewValue] nvarchar(2000) NULL,
        [ChangedById] nvarchar(150) NULL,
        [ChangedAt] datetime2 NOT NULL,
        [ChangeReason] nvarchar(500) NULL,
        CONSTRAINT [PK_ImprovementChangeLogs] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImprovementChangeLogs_ImprovementInitiatives_ImprovementId] FOREIGN KEY ([ImprovementId]) REFERENCES [ImprovementInitiatives] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE INDEX [IX_ImprovementInitiatives_StrategicObjectiveId] ON [ImprovementInitiatives] ([StrategicObjectiveId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE INDEX [IX_ImprovementAssets_AssetId] ON [ImprovementAssets] ([AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE INDEX [IX_ImprovementAssets_ImprovementId] ON [ImprovementAssets] ([ImprovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ImprovementAssets_ImprovementId_AssetId] ON [ImprovementAssets] ([ImprovementId], [AssetId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE INDEX [IX_ImprovementBenefitsReviews_DueDate] ON [ImprovementBenefitsReviews] ([DueDate]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE INDEX [IX_ImprovementBenefitsReviews_ImprovementId] ON [ImprovementBenefitsReviews] ([ImprovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ImprovementBenefitsReviews_ImprovementId_Period] ON [ImprovementBenefitsReviews] ([ImprovementId], [Period]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE INDEX [IX_ImprovementChangeLogs_ChangedAt] ON [ImprovementChangeLogs] ([ChangedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    CREATE INDEX [IX_ImprovementChangeLogs_ImprovementId] ON [ImprovementChangeLogs] ([ImprovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    ALTER TABLE [ImprovementInitiatives] ADD CONSTRAINT [FK_ImprovementInitiatives_StrategicObjectives_StrategicObjectiveId] FOREIGN KEY ([StrategicObjectiveId]) REFERENCES [StrategicObjectives] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503172944_AuditBatchB_Structural'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260503172944_AuditBatchB_Structural', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503173958_AuditBatchC_Governance'
)
BEGIN
    CREATE TABLE [ReviewCycles] (
        [Id] nvarchar(450) NOT NULL,
        [Name] nvarchar(150) NOT NULL,
        [NameAr] nvarchar(150) NULL,
        [Cadence] nvarchar(20) NOT NULL,
        [OwningCommittee] nvarchar(200) NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NULL,
        [IsActive] bit NOT NULL,
        [Notes] nvarchar(1000) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ReviewCycles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503173958_AuditBatchC_Governance'
)
BEGIN
    CREATE TABLE [ImprovementReviewCycleAssignments] (
        [Id] nvarchar(450) NOT NULL,
        [ImprovementId] nvarchar(450) NOT NULL,
        [ReviewCycleId] nvarchar(450) NOT NULL,
        [LastGeneratedDate] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ImprovementReviewCycleAssignments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImprovementReviewCycleAssignments_ImprovementInitiatives_ImprovementId] FOREIGN KEY ([ImprovementId]) REFERENCES [ImprovementInitiatives] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ImprovementReviewCycleAssignments_ReviewCycles_ReviewCycleId] FOREIGN KEY ([ReviewCycleId]) REFERENCES [ReviewCycles] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503173958_AuditBatchC_Governance'
)
BEGIN
    CREATE INDEX [IX_ImprovementReviewCycleAssignments_ImprovementId] ON [ImprovementReviewCycleAssignments] ([ImprovementId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503173958_AuditBatchC_Governance'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ImprovementReviewCycleAssignments_ImprovementId_ReviewCycleId] ON [ImprovementReviewCycleAssignments] ([ImprovementId], [ReviewCycleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503173958_AuditBatchC_Governance'
)
BEGIN
    CREATE INDEX [IX_ImprovementReviewCycleAssignments_ReviewCycleId] ON [ImprovementReviewCycleAssignments] ([ReviewCycleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503173958_AuditBatchC_Governance'
)
BEGIN
    CREATE INDEX [IX_ReviewCycles_IsActive] ON [ReviewCycles] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503173958_AuditBatchC_Governance'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260503173958_AuditBatchC_Governance', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503174553_AuditBatchD_KpiLibrary'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD [KpiDefinitionId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503174553_AuditBatchD_KpiLibrary'
)
BEGIN
    CREATE TABLE [KpiDefinitions] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(50) NOT NULL,
        [UnitOfMeasure] nvarchar(50) NOT NULL,
        [Direction] nvarchar(20) NOT NULL,
        [DefaultType] nvarchar(30) NOT NULL,
        [OwningUnitId] nvarchar(450) NULL,
        [IsActive] bit NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_KpiDefinitions] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503174553_AuditBatchD_KpiLibrary'
)
BEGIN
    CREATE INDEX [IX_ImprovementMeasurements_KpiDefinitionId] ON [ImprovementMeasurements] ([KpiDefinitionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503174553_AuditBatchD_KpiLibrary'
)
BEGIN
    CREATE UNIQUE INDEX [IX_KpiDefinitions_Code] ON [KpiDefinitions] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503174553_AuditBatchD_KpiLibrary'
)
BEGIN
    CREATE INDEX [IX_KpiDefinitions_IsActive] ON [KpiDefinitions] ([IsActive]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503174553_AuditBatchD_KpiLibrary'
)
BEGIN
    CREATE INDEX [IX_KpiDefinitions_IsDeleted] ON [KpiDefinitions] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503174553_AuditBatchD_KpiLibrary'
)
BEGIN
    ALTER TABLE [ImprovementMeasurements] ADD CONSTRAINT [FK_ImprovementMeasurements_KpiDefinitions_KpiDefinitionId] FOREIGN KEY ([KpiDefinitionId]) REFERENCES [KpiDefinitions] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260503174553_AuditBatchD_KpiLibrary'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260503174553_AuditBatchD_KpiLibrary', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504165030_DropOwningUnitFromCategoryAndProcessGroup'
)
BEGIN
    ALTER TABLE [Categories] DROP CONSTRAINT [FK_Categories_OrganizationUnits_OwningUnitId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504165030_DropOwningUnitFromCategoryAndProcessGroup'
)
BEGIN
    ALTER TABLE [ProcessGroups] DROP CONSTRAINT [FK_ProcessGroups_OrganizationUnits_OwningUnitId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504165030_DropOwningUnitFromCategoryAndProcessGroup'
)
BEGIN
    DROP INDEX [IX_ProcessGroups_OwningUnitId] ON [ProcessGroups];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504165030_DropOwningUnitFromCategoryAndProcessGroup'
)
BEGIN
    DROP INDEX [IX_Categories_OwningUnitId] ON [Categories];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504165030_DropOwningUnitFromCategoryAndProcessGroup'
)
BEGIN
    DECLARE @var3 sysname;
    SELECT @var3 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessGroups]') AND [c].[name] = N'OwningUnitId');
    IF @var3 IS NOT NULL EXEC(N'ALTER TABLE [ProcessGroups] DROP CONSTRAINT [' + @var3 + '];');
    ALTER TABLE [ProcessGroups] DROP COLUMN [OwningUnitId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504165030_DropOwningUnitFromCategoryAndProcessGroup'
)
BEGIN
    DECLARE @var4 sysname;
    SELECT @var4 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'OwningUnitId');
    IF @var4 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var4 + '];');
    ALTER TABLE [Categories] DROP COLUMN [OwningUnitId];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260504165030_DropOwningUnitFromCategoryAndProcessGroup'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260504165030_DropOwningUnitFromCategoryAndProcessGroup', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505104917_AddImprovementDrafts'
)
BEGIN
    CREATE TABLE [ImprovementDrafts] (
        [Id] nvarchar(450) NOT NULL,
        [OwnerId] nvarchar(256) NOT NULL,
        [Title] nvarchar(500) NOT NULL,
        [PayloadJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ImprovementDrafts] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505104917_AddImprovementDrafts'
)
BEGIN
    CREATE INDEX [IX_ImprovementDrafts_OwnerId_UpdatedAt] ON [ImprovementDrafts] ([OwnerId], [UpdatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505104917_AddImprovementDrafts'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505104917_AddImprovementDrafts', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505172002_BackfillMeasuringPeriodMonthly'
)
BEGIN
    UPDATE [ImprovementMeasurements] SET [MeasuringPeriod] = 'Monthly' WHERE [MeasuringPeriod] IS NULL OR LTRIM(RTRIM([MeasuringPeriod])) = ''
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260505172002_BackfillMeasuringPeriodMonthly'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260505172002_BackfillMeasuringPeriodMonthly', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516120033_AddBpmnLanes'
)
BEGIN
    CREATE TABLE [BpmnLanes] (
        [Id] nvarchar(450) NOT NULL,
        [ProcessId] nvarchar(450) NOT NULL,
        [BpmnId] nvarchar(256) NOT NULL,
        [Name] nvarchar(512) NOT NULL,
        [OrganizationUnitId] nvarchar(450) NULL,
        [MatchMethod] nvarchar(32) NOT NULL,
        [MatchedAt] datetime2 NULL,
        [MatchedById] nvarchar(256) NULL,
        [FlowNodeRefsJson] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_BpmnLanes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_BpmnLanes_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_BpmnLanes_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516120033_AddBpmnLanes'
)
BEGIN
    CREATE INDEX [IX_BpmnLanes_OrganizationUnitId] ON [BpmnLanes] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516120033_AddBpmnLanes'
)
BEGIN
    CREATE UNIQUE INDEX [IX_BpmnLanes_ProcessId_BpmnId] ON [BpmnLanes] ([ProcessId], [BpmnId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516120033_AddBpmnLanes'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516120033_AddBpmnLanes', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [Bedrooms] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [BuiltUpAreaSqm] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [ConstructionStatus] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [District] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [Emirate] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [Floors] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [GpsLatitude] decimal(9,6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [GpsLongitude] decimal(9,6) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [LandAreaSqm] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [ParentProjectId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [PlotNumber] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [TitleDeedNumber] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [Units] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    CREATE INDEX [IX_Assets_ConstructionStatus] ON [Assets] ([ConstructionStatus]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    CREATE INDEX [IX_Assets_ParentProjectId] ON [Assets] ([ParentProjectId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD CONSTRAINT [FK_Assets_Assets_ParentProjectId] FOREIGN KEY ([ParentProjectId]) REFERENCES [Assets] ([Id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516165515_AddHousingAssetFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516165515_AddHousingAssetFields', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [Classification] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [DataCustodianUserId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [DataFormat] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [DataOwnerUserId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [RecordCount] bigint NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [RegulatoryTags] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [RetentionMonths] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    ALTER TABLE [Assets] ADD [StorageSystem] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    CREATE INDEX [IX_Assets_Classification] ON [Assets] ([Classification]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    CREATE INDEX [IX_Assets_DataOwnerUserId] ON [Assets] ([DataOwnerUserId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516171646_AddInformationAssetFields'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516171646_AddInformationAssetFields', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516175422_AddNotificationDedupKey'
)
BEGIN
    ALTER TABLE [Notifications] ADD [DedupKey] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516175422_AddNotificationDedupKey'
)
BEGIN
    CREATE INDEX [IX_Notifications_UserId_DedupKey] ON [Notifications] ([UserId], [DedupKey]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260516175422_AddNotificationDedupKey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260516175422_AddNotificationDedupKey', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518072435_AddOrganizationUnitResponsibilities'
)
BEGIN
    CREATE TABLE [OrganizationUnitResponsibilities] (
        [Id] nvarchar(450) NOT NULL,
        [OrganizationUnitId] nvarchar(450) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_OrganizationUnitResponsibilities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_OrganizationUnitResponsibilities_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518072435_AddOrganizationUnitResponsibilities'
)
BEGIN
    CREATE INDEX [IX_OrganizationUnitResponsibilities_IsDeleted] ON [OrganizationUnitResponsibilities] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518072435_AddOrganizationUnitResponsibilities'
)
BEGIN
    CREATE INDEX [IX_OrganizationUnitResponsibilities_OrganizationUnitId] ON [OrganizationUnitResponsibilities] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518072435_AddOrganizationUnitResponsibilities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518072435_AddOrganizationUnitResponsibilities', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518080554_AddProcessAndServiceResponsibilityLinks'
)
BEGIN
    CREATE TABLE [ProcessResponsibilities] (
        [ProcessId] nvarchar(450) NOT NULL,
        [ResponsibilityId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ProcessResponsibilities] PRIMARY KEY ([ProcessId], [ResponsibilityId]),
        CONSTRAINT [FK_ProcessResponsibilities_OrganizationUnitResponsibilities_ResponsibilityId] FOREIGN KEY ([ResponsibilityId]) REFERENCES [OrganizationUnitResponsibilities] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ProcessResponsibilities_Processes_ProcessId] FOREIGN KEY ([ProcessId]) REFERENCES [Processes] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518080554_AddProcessAndServiceResponsibilityLinks'
)
BEGIN
    CREATE TABLE [ServiceResponsibilities] (
        [ServiceId] nvarchar(450) NOT NULL,
        [ResponsibilityId] nvarchar(450) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_ServiceResponsibilities] PRIMARY KEY ([ServiceId], [ResponsibilityId]),
        CONSTRAINT [FK_ServiceResponsibilities_OrganizationUnitResponsibilities_ResponsibilityId] FOREIGN KEY ([ResponsibilityId]) REFERENCES [OrganizationUnitResponsibilities] ([Id]) ON DELETE CASCADE,
        CONSTRAINT [FK_ServiceResponsibilities_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518080554_AddProcessAndServiceResponsibilityLinks'
)
BEGIN
    CREATE INDEX [IX_ProcessResponsibilities_ResponsibilityId] ON [ProcessResponsibilities] ([ResponsibilityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518080554_AddProcessAndServiceResponsibilityLinks'
)
BEGIN
    CREATE INDEX [IX_ServiceResponsibilities_ResponsibilityId] ON [ServiceResponsibilities] ([ResponsibilityId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518080554_AddProcessAndServiceResponsibilityLinks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518080554_AddProcessAndServiceResponsibilityLinks', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518085006_AddServiceCatalogInfo'
)
BEGIN
    CREATE TABLE [ServiceCatalogInfos] (
        [Id] nvarchar(450) NOT NULL,
        [ServiceId] nvarchar(450) NOT NULL,
        [DurationEn] nvarchar(max) NULL,
        [DurationAr] nvarchar(max) NULL,
        [FeesEn] nvarchar(max) NULL,
        [FeesAr] nvarchar(max) NULL,
        [ChannelsEn] nvarchar(max) NULL,
        [ChannelsAr] nvarchar(max) NULL,
        [TargetAudienceEn] nvarchar(max) NULL,
        [TargetAudienceAr] nvarchar(max) NULL,
        [PreConditionsEn] nvarchar(max) NULL,
        [PreConditionsAr] nvarchar(max) NULL,
        [PoliciesEn] nvarchar(max) NULL,
        [PoliciesAr] nvarchar(max) NULL,
        [ProcedureEn] nvarchar(max) NULL,
        [ProcedureAr] nvarchar(max) NULL,
        [CategoryEn] nvarchar(max) NULL,
        [CategoryAr] nvarchar(max) NULL,
        [IsPublished] bit NOT NULL,
        [PublishedAt] datetime2 NULL,
        [PublishedById] nvarchar(max) NULL,
        [SourceReference] nvarchar(256) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        CONSTRAINT [PK_ServiceCatalogInfos] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ServiceCatalogInfos_Services_ServiceId] FOREIGN KEY ([ServiceId]) REFERENCES [Services] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518085006_AddServiceCatalogInfo'
)
BEGIN
    CREATE INDEX [IX_ServiceCatalogInfos_IsPublished] ON [ServiceCatalogInfos] ([IsPublished]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518085006_AddServiceCatalogInfo'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ServiceCatalogInfos_ServiceId] ON [ServiceCatalogInfos] ([ServiceId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518085006_AddServiceCatalogInfo'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518085006_AddServiceCatalogInfo', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518090741_PromoteServiceCategoryToServiceTable'
)
BEGIN
    ALTER TABLE [Services] ADD [CategoryAr] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518090741_PromoteServiceCategoryToServiceTable'
)
BEGIN
    ALTER TABLE [Services] ADD [CategoryEn] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518090741_PromoteServiceCategoryToServiceTable'
)
BEGIN

                    UPDATE s
                    SET    s.CategoryEn = ci.CategoryEn,
                           s.CategoryAr = ci.CategoryAr
                    FROM   Services s
                    INNER JOIN ServiceCatalogInfos ci ON ci.ServiceId = s.Id
                    WHERE  ci.CategoryEn IS NOT NULL OR ci.CategoryAr IS NOT NULL;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518090741_PromoteServiceCategoryToServiceTable'
)
BEGIN
    DECLARE @var5 sysname;
    SELECT @var5 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'CategoryAr');
    IF @var5 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var5 + '];');
    ALTER TABLE [ServiceCatalogInfos] DROP COLUMN [CategoryAr];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518090741_PromoteServiceCategoryToServiceTable'
)
BEGIN
    DECLARE @var6 sysname;
    SELECT @var6 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'CategoryEn');
    IF @var6 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var6 + '];');
    ALTER TABLE [ServiceCatalogInfos] DROP COLUMN [CategoryEn];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518090741_PromoteServiceCategoryToServiceTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518090741_PromoteServiceCategoryToServiceTable', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518132616_DropDisplayOrderLegacyCodeFromL1L2'
)
BEGIN
    DROP INDEX [IX_Categories_DisplayOrder] ON [Categories];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518132616_DropDisplayOrderLegacyCodeFromL1L2'
)
BEGIN
    DECLARE @var7 sysname;
    SELECT @var7 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessGroups]') AND [c].[name] = N'DisplayOrder');
    IF @var7 IS NOT NULL EXEC(N'ALTER TABLE [ProcessGroups] DROP CONSTRAINT [' + @var7 + '];');
    ALTER TABLE [ProcessGroups] DROP COLUMN [DisplayOrder];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518132616_DropDisplayOrderLegacyCodeFromL1L2'
)
BEGIN
    DECLARE @var8 sysname;
    SELECT @var8 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessGroups]') AND [c].[name] = N'LegacyCode');
    IF @var8 IS NOT NULL EXEC(N'ALTER TABLE [ProcessGroups] DROP CONSTRAINT [' + @var8 + '];');
    ALTER TABLE [ProcessGroups] DROP COLUMN [LegacyCode];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518132616_DropDisplayOrderLegacyCodeFromL1L2'
)
BEGIN
    DECLARE @var9 sysname;
    SELECT @var9 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'DisplayOrder');
    IF @var9 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var9 + '];');
    ALTER TABLE [Categories] DROP COLUMN [DisplayOrder];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518132616_DropDisplayOrderLegacyCodeFromL1L2'
)
BEGIN
    DECLARE @var10 sysname;
    SELECT @var10 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'LegacyCode');
    IF @var10 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var10 + '];');
    ALTER TABLE [Categories] DROP COLUMN [LegacyCode];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518132616_DropDisplayOrderLegacyCodeFromL1L2'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518132616_DropDisplayOrderLegacyCodeFromL1L2', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518134122_AddOrganizationUnitType'
)
BEGIN
    ALTER TABLE [OrganizationUnits] ADD [UnitType] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518134122_AddOrganizationUnitType'
)
BEGIN

                    UPDATE [OrganizationUnits]
                    SET [UnitType] = [Level]
                    WHERE [Level] IN (0, 1, 2) AND [UnitType] IS NULL;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518134122_AddOrganizationUnitType'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518134122_AddOrganizationUnitType', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    ALTER TABLE [TaskRacis] ADD [JobRoleId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    ALTER TABLE [ProcessRacis] ADD [JobRoleId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    ALTER TABLE [ActivityRacis] ADD [JobRoleId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    CREATE TABLE [JobRoles] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NULL,
        [Category] nvarchar(max) NULL,
        [IsLeadership] bit NOT NULL,
        [DisplayOrder] int NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_JobRoles] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    CREATE INDEX [IX_TaskRacis_JobRoleId] ON [TaskRacis] ([JobRoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    CREATE INDEX [IX_ProcessRacis_JobRoleId] ON [ProcessRacis] ([JobRoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    CREATE INDEX [IX_ActivityRacis_JobRoleId] ON [ActivityRacis] ([JobRoleId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    CREATE INDEX [IX_JobRoles_Code] ON [JobRoles] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    CREATE INDEX [IX_JobRoles_IsDeleted] ON [JobRoles] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    ALTER TABLE [ActivityRacis] ADD CONSTRAINT [FK_ActivityRacis_JobRoles_JobRoleId] FOREIGN KEY ([JobRoleId]) REFERENCES [JobRoles] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    ALTER TABLE [ProcessRacis] ADD CONSTRAINT [FK_ProcessRacis_JobRoles_JobRoleId] FOREIGN KEY ([JobRoleId]) REFERENCES [JobRoles] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    ALTER TABLE [TaskRacis] ADD CONSTRAINT [FK_TaskRacis_JobRoles_JobRoleId] FOREIGN KEY ([JobRoleId]) REFERENCES [JobRoles] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518140442_AddJobRoles'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518140442_AddJobRoles', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518151354_MarkImprovementVersionConcurrencyToken'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518151354_MarkImprovementVersionConcurrencyToken', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    DECLARE @var11 sysname;
    SELECT @var11 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'ChannelsAr');
    IF @var11 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var11 + '];');
    ALTER TABLE [ServiceCatalogInfos] DROP COLUMN [ChannelsAr];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    DECLARE @var12 sysname;
    SELECT @var12 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'ChannelsEn');
    IF @var12 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var12 + '];');
    ALTER TABLE [ServiceCatalogInfos] DROP COLUMN [ChannelsEn];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    DECLARE @var13 sysname;
    SELECT @var13 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'DurationAr');
    IF @var13 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var13 + '];');
    ALTER TABLE [ServiceCatalogInfos] DROP COLUMN [DurationAr];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    DECLARE @var14 sysname;
    SELECT @var14 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'DurationEn');
    IF @var14 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var14 + '];');
    ALTER TABLE [ServiceCatalogInfos] DROP COLUMN [DurationEn];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    DECLARE @var15 sysname;
    SELECT @var15 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'FeesAr');
    IF @var15 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var15 + '];');
    ALTER TABLE [ServiceCatalogInfos] DROP COLUMN [FeesAr];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    DECLARE @var16 sysname;
    SELECT @var16 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'FeesEn');
    IF @var16 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var16 + '];');
    ALTER TABLE [ServiceCatalogInfos] DROP COLUMN [FeesEn];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    ALTER TABLE [ServiceCatalogInfos] ADD [DeliveryChannels] nvarchar(500) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    ALTER TABLE [ServiceCatalogInfos] ADD [DurationUnit] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    ALTER TABLE [ServiceCatalogInfos] ADD [DurationValue] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    ALTER TABLE [ServiceCatalogInfos] ADD [FeeAmount] decimal(18,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    ALTER TABLE [ServiceCatalogInfos] ADD [FeeNote] nvarchar(200) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    ALTER TABLE [ServiceCatalogInfos] ADD [IsFree] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518161000_StructureServiceCatalogDurationFeeChannels'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518161000_StructureServiceCatalogDurationFeeChannels', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518163646_AddJobRoleOrganizationUnitScope'
)
BEGIN
    ALTER TABLE [JobRoles] ADD [OrganizationUnitId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518163646_AddJobRoleOrganizationUnitScope'
)
BEGIN
    CREATE INDEX [IX_JobRoles_OrganizationUnitId] ON [JobRoles] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518163646_AddJobRoleOrganizationUnitScope'
)
BEGIN
    ALTER TABLE [JobRoles] ADD CONSTRAINT [FK_JobRoles_OrganizationUnits_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [OrganizationUnits] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260518163646_AddJobRoleOrganizationUnitScope'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260518163646_AddJobRoleOrganizationUnitScope', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519145402_AddServiceCategoryLookup'
)
BEGIN
    ALTER TABLE [Services] ADD [ServiceCategoryId] nvarchar(450) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519145402_AddServiceCategoryLookup'
)
BEGIN
    CREATE TABLE [ServiceCategories] (
        [Id] nvarchar(450) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [IsActive] bit NOT NULL,
        [NameEn] nvarchar(max) NOT NULL,
        [DescriptionEn] nvarchar(max) NULL,
        [NameAr] nvarchar(max) NOT NULL,
        [DescriptionAr] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [CreatedById] nvarchar(max) NULL,
        [UpdatedById] nvarchar(max) NULL,
        [Version] int NOT NULL,
        [IsDeleted] bit NOT NULL,
        [DeletedAt] datetime2 NULL,
        CONSTRAINT [PK_ServiceCategories] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519145402_AddServiceCategoryLookup'
)
BEGIN
    CREATE INDEX [IX_Services_ServiceCategoryId] ON [Services] ([ServiceCategoryId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519145402_AddServiceCategoryLookup'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ServiceCategories_Code] ON [ServiceCategories] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519145402_AddServiceCategoryLookup'
)
BEGIN
    CREATE INDEX [IX_ServiceCategories_IsDeleted] ON [ServiceCategories] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519145402_AddServiceCategoryLookup'
)
BEGIN
    ALTER TABLE [Services] ADD CONSTRAINT [FK_Services_ServiceCategories_ServiceCategoryId] FOREIGN KEY ([ServiceCategoryId]) REFERENCES [ServiceCategories] ([Id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519145402_AddServiceCategoryLookup'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519145402_AddServiceCategoryLookup', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519151622_DropLegacyServiceCategoryColumns'
)
BEGIN
    DECLARE @var17 sysname;
    SELECT @var17 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Services]') AND [c].[name] = N'CategoryAr');
    IF @var17 IS NOT NULL EXEC(N'ALTER TABLE [Services] DROP CONSTRAINT [' + @var17 + '];');
    ALTER TABLE [Services] DROP COLUMN [CategoryAr];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519151622_DropLegacyServiceCategoryColumns'
)
BEGIN
    DECLARE @var18 sysname;
    SELECT @var18 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Services]') AND [c].[name] = N'CategoryEn');
    IF @var18 IS NOT NULL EXEC(N'ALTER TABLE [Services] DROP CONSTRAINT [' + @var18 + '];');
    ALTER TABLE [Services] DROP COLUMN [CategoryEn];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519151622_DropLegacyServiceCategoryColumns'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519151622_DropLegacyServiceCategoryColumns', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var19 sysname;
    SELECT @var19 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadScenarios]') AND [c].[name] = N'GrowthRatePercent');
    IF @var19 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadScenarios] DROP CONSTRAINT [' + @var19 + '];');
    ALTER TABLE [WorkloadScenarios] ALTER COLUMN [GrowthRatePercent] decimal(5,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var20 sysname;
    SELECT @var20 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadLineItems]') AND [c].[name] = N'SimpleVolumePercent');
    IF @var20 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadLineItems] DROP CONSTRAINT [' + @var20 + '];');
    ALTER TABLE [WorkloadLineItems] ALTER COLUMN [SimpleVolumePercent] decimal(5,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var21 sysname;
    SELECT @var21 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadLineItems]') AND [c].[name] = N'SimpleMult');
    IF @var21 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadLineItems] DROP CONSTRAINT [' + @var21 + '];');
    ALTER TABLE [WorkloadLineItems] ALTER COLUMN [SimpleMult] decimal(4,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var22 sysname;
    SELECT @var22 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadLineItems]') AND [c].[name] = N'MediumVolumePercent');
    IF @var22 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadLineItems] DROP CONSTRAINT [' + @var22 + '];');
    ALTER TABLE [WorkloadLineItems] ALTER COLUMN [MediumVolumePercent] decimal(5,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var23 sysname;
    SELECT @var23 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadLineItems]') AND [c].[name] = N'MediumMult');
    IF @var23 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadLineItems] DROP CONSTRAINT [' + @var23 + '];');
    ALTER TABLE [WorkloadLineItems] ALTER COLUMN [MediumMult] decimal(4,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var24 sysname;
    SELECT @var24 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadLineItems]') AND [c].[name] = N'ComplexVolumePercent');
    IF @var24 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadLineItems] DROP CONSTRAINT [' + @var24 + '];');
    ALTER TABLE [WorkloadLineItems] ALTER COLUMN [ComplexVolumePercent] decimal(5,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var25 sysname;
    SELECT @var25 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadLineItems]') AND [c].[name] = N'ComplexMult');
    IF @var25 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadLineItems] DROP CONSTRAINT [' + @var25 + '];');
    ALTER TABLE [WorkloadLineItems] ALTER COLUMN [ComplexMult] decimal(4,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var26 sysname;
    SELECT @var26 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadLineItems]') AND [c].[name] = N'AvgProcessingTimeMinutes');
    IF @var26 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadLineItems] DROP CONSTRAINT [' + @var26 + '];');
    ALTER TABLE [WorkloadLineItems] ALTER COLUMN [AvgProcessingTimeMinutes] decimal(8,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var27 sysname;
    SELECT @var27 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadConfigs]') AND [c].[name] = N'WorkingHoursPerDay');
    IF @var27 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadConfigs] DROP CONSTRAINT [' + @var27 + '];');
    ALTER TABLE [WorkloadConfigs] ALTER COLUMN [WorkingHoursPerDay] decimal(5,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var28 sysname;
    SELECT @var28 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadConfigs]') AND [c].[name] = N'TargetUtilizationRate');
    IF @var28 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadConfigs] DROP CONSTRAINT [' + @var28 + '];');
    ALTER TABLE [WorkloadConfigs] ALTER COLUMN [TargetUtilizationRate] decimal(5,4) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var29 sysname;
    SELECT @var29 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadConfigs]') AND [c].[name] = N'AdminOverheadPercent');
    IF @var29 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadConfigs] DROP CONSTRAINT [' + @var29 + '];');
    ALTER TABLE [WorkloadConfigs] ALTER COLUMN [AdminOverheadPercent] decimal(5,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var30 sysname;
    SELECT @var30 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StrategicObjectives]') AND [c].[name] = N'TargetValue');
    IF @var30 IS NOT NULL EXEC(N'ALTER TABLE [StrategicObjectives] DROP CONSTRAINT [' + @var30 + '];');
    ALTER TABLE [StrategicObjectives] ALTER COLUMN [TargetValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var31 sysname;
    SELECT @var31 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StrategicObjectives]') AND [c].[name] = N'CurrentValue');
    IF @var31 IS NOT NULL EXEC(N'ALTER TABLE [StrategicObjectives] DROP CONSTRAINT [' + @var31 + '];');
    ALTER TABLE [StrategicObjectives] ALTER COLUMN [CurrentValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var32 sysname;
    SELECT @var32 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SLADefinitions]') AND [c].[name] = N'WarningThreshold');
    IF @var32 IS NOT NULL EXEC(N'ALTER TABLE [SLADefinitions] DROP CONSTRAINT [' + @var32 + '];');
    ALTER TABLE [SLADefinitions] ALTER COLUMN [WarningThreshold] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var33 sysname;
    SELECT @var33 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SLADefinitions]') AND [c].[name] = N'TargetValue');
    IF @var33 IS NOT NULL EXEC(N'ALTER TABLE [SLADefinitions] DROP CONSTRAINT [' + @var33 + '];');
    ALTER TABLE [SLADefinitions] ALTER COLUMN [TargetValue] decimal(18,4) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var34 sysname;
    SELECT @var34 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SLABreaches]') AND [c].[name] = N'VariancePercentage');
    IF @var34 IS NOT NULL EXEC(N'ALTER TABLE [SLABreaches] DROP CONSTRAINT [' + @var34 + '];');
    ALTER TABLE [SLABreaches] ALTER COLUMN [VariancePercentage] decimal(5,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var35 sysname;
    SELECT @var35 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SLABreaches]') AND [c].[name] = N'Variance');
    IF @var35 IS NOT NULL EXEC(N'ALTER TABLE [SLABreaches] DROP CONSTRAINT [' + @var35 + '];');
    ALTER TABLE [SLABreaches] ALTER COLUMN [Variance] decimal(18,4) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var36 sysname;
    SELECT @var36 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SLABreaches]') AND [c].[name] = N'TargetValue');
    IF @var36 IS NOT NULL EXEC(N'ALTER TABLE [SLABreaches] DROP CONSTRAINT [' + @var36 + '];');
    ALTER TABLE [SLABreaches] ALTER COLUMN [TargetValue] decimal(18,4) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var37 sysname;
    SELECT @var37 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SLABreaches]') AND [c].[name] = N'ActualValue');
    IF @var37 IS NOT NULL EXEC(N'ALTER TABLE [SLABreaches] DROP CONSTRAINT [' + @var37 + '];');
    ALTER TABLE [SLABreaches] ALTER COLUMN [ActualValue] decimal(18,4) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var38 sysname;
    SELECT @var38 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Services]') AND [c].[name] = N'TargetDeliveryDays');
    IF @var38 IS NOT NULL EXEC(N'ALTER TABLE [Services] DROP CONSTRAINT [' + @var38 + '];');
    ALTER TABLE [Services] ALTER COLUMN [TargetDeliveryDays] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var39 sysname;
    SELECT @var39 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Services]') AND [c].[name] = N'EstimatedDuration');
    IF @var39 IS NOT NULL EXEC(N'ALTER TABLE [Services] DROP CONSTRAINT [' + @var39 + '];');
    ALTER TABLE [Services] ALTER COLUMN [EstimatedDuration] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var40 sysname;
    SELECT @var40 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Services]') AND [c].[name] = N'CustomerSatisfactionScore');
    IF @var40 IS NOT NULL EXEC(N'ALTER TABLE [Services] DROP CONSTRAINT [' + @var40 + '];');
    ALTER TABLE [Services] ALTER COLUMN [CustomerSatisfactionScore] decimal(5,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var41 sysname;
    SELECT @var41 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Services]') AND [c].[name] = N'ActualDuration');
    IF @var41 IS NOT NULL EXEC(N'ALTER TABLE [Services] DROP CONSTRAINT [' + @var41 + '];');
    ALTER TABLE [Services] ALTER COLUMN [ActualDuration] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var42 sysname;
    SELECT @var42 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Services]') AND [c].[name] = N'ActualDeliveryDays');
    IF @var42 IS NOT NULL EXEC(N'ALTER TABLE [Services] DROP CONSTRAINT [' + @var42 + '];');
    ALTER TABLE [Services] ALTER COLUMN [ActualDeliveryDays] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var43 sysname;
    SELECT @var43 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceMeasurements]') AND [c].[name] = N'TargetValue');
    IF @var43 IS NOT NULL EXEC(N'ALTER TABLE [ServiceMeasurements] DROP CONSTRAINT [' + @var43 + '];');
    ALTER TABLE [ServiceMeasurements] ALTER COLUMN [TargetValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var44 sysname;
    SELECT @var44 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceMeasurements]') AND [c].[name] = N'MinValue');
    IF @var44 IS NOT NULL EXEC(N'ALTER TABLE [ServiceMeasurements] DROP CONSTRAINT [' + @var44 + '];');
    ALTER TABLE [ServiceMeasurements] ALTER COLUMN [MinValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var45 sysname;
    SELECT @var45 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceMeasurements]') AND [c].[name] = N'MaxValue');
    IF @var45 IS NOT NULL EXEC(N'ALTER TABLE [ServiceMeasurements] DROP CONSTRAINT [' + @var45 + '];');
    ALTER TABLE [ServiceMeasurements] ALTER COLUMN [MaxValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var46 sysname;
    SELECT @var46 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceMeasurements]') AND [c].[name] = N'ActualValue');
    IF @var46 IS NOT NULL EXEC(N'ALTER TABLE [ServiceMeasurements] DROP CONSTRAINT [' + @var46 + '];');
    ALTER TABLE [ServiceMeasurements] ALTER COLUMN [ActualValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var47 sysname;
    SELECT @var47 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'DurationValue');
    IF @var47 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var47 + '];');
    ALTER TABLE [ServiceCatalogInfos] ALTER COLUMN [DurationValue] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var48 sysname;
    SELECT @var48 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessTasks]') AND [c].[name] = N'EstimatedDuration');
    IF @var48 IS NOT NULL EXEC(N'ALTER TABLE [ProcessTasks] DROP CONSTRAINT [' + @var48 + '];');
    ALTER TABLE [ProcessTasks] ALTER COLUMN [EstimatedDuration] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var49 sysname;
    SELECT @var49 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessTasks]') AND [c].[name] = N'ActualDuration');
    IF @var49 IS NOT NULL EXEC(N'ALTER TABLE [ProcessTasks] DROP CONSTRAINT [' + @var49 + '];');
    ALTER TABLE [ProcessTasks] ALTER COLUMN [ActualDuration] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var50 sysname;
    SELECT @var50 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMeasurements]') AND [c].[name] = N'TargetValue');
    IF @var50 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMeasurements] DROP CONSTRAINT [' + @var50 + '];');
    ALTER TABLE [ProcessMeasurements] ALTER COLUMN [TargetValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var51 sysname;
    SELECT @var51 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMeasurements]') AND [c].[name] = N'MinValue');
    IF @var51 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMeasurements] DROP CONSTRAINT [' + @var51 + '];');
    ALTER TABLE [ProcessMeasurements] ALTER COLUMN [MinValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var52 sysname;
    SELECT @var52 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMeasurements]') AND [c].[name] = N'MaxValue');
    IF @var52 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMeasurements] DROP CONSTRAINT [' + @var52 + '];');
    ALTER TABLE [ProcessMeasurements] ALTER COLUMN [MaxValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var53 sysname;
    SELECT @var53 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMeasurements]') AND [c].[name] = N'ActualValue');
    IF @var53 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMeasurements] DROP CONSTRAINT [' + @var53 + '];');
    ALTER TABLE [ProcessMeasurements] ALTER COLUMN [ActualValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var54 sysname;
    SELECT @var54 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMaturityAssessments]') AND [c].[name] = N'ToolsAndTechnology');
    IF @var54 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMaturityAssessments] DROP CONSTRAINT [' + @var54 + '];');
    ALTER TABLE [ProcessMaturityAssessments] ALTER COLUMN [ToolsAndTechnology] decimal(4,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var55 sysname;
    SELECT @var55 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMaturityAssessments]') AND [c].[name] = N'StrategicAlignment');
    IF @var55 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMaturityAssessments] DROP CONSTRAINT [' + @var55 + '];');
    ALTER TABLE [ProcessMaturityAssessments] ALTER COLUMN [StrategicAlignment] decimal(4,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var56 sysname;
    SELECT @var56 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMaturityAssessments]') AND [c].[name] = N'ProcessPerformance');
    IF @var56 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMaturityAssessments] DROP CONSTRAINT [' + @var56 + '];');
    ALTER TABLE [ProcessMaturityAssessments] ALTER COLUMN [ProcessPerformance] decimal(4,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var57 sysname;
    SELECT @var57 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMaturityAssessments]') AND [c].[name] = N'ProcessModels');
    IF @var57 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMaturityAssessments] DROP CONSTRAINT [' + @var57 + '];');
    ALTER TABLE [ProcessMaturityAssessments] ALTER COLUMN [ProcessModels] decimal(4,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var58 sysname;
    SELECT @var58 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMaturityAssessments]') AND [c].[name] = N'ProcessImprovement');
    IF @var58 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMaturityAssessments] DROP CONSTRAINT [' + @var58 + '];');
    ALTER TABLE [ProcessMaturityAssessments] ALTER COLUMN [ProcessImprovement] decimal(4,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var59 sysname;
    SELECT @var59 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMaturityAssessments]') AND [c].[name] = N'OverallScore');
    IF @var59 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMaturityAssessments] DROP CONSTRAINT [' + @var59 + '];');
    ALTER TABLE [ProcessMaturityAssessments] ALTER COLUMN [OverallScore] decimal(4,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var60 sysname;
    SELECT @var60 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMaturityAssessments]') AND [c].[name] = N'Governance');
    IF @var60 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMaturityAssessments] DROP CONSTRAINT [' + @var60 + '];');
    ALTER TABLE [ProcessMaturityAssessments] ALTER COLUMN [Governance] decimal(4,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var61 sysname;
    SELECT @var61 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessMaturityAssessments]') AND [c].[name] = N'ChangeManagement');
    IF @var61 IS NOT NULL EXEC(N'ALTER TABLE [ProcessMaturityAssessments] DROP CONSTRAINT [' + @var61 + '];');
    ALTER TABLE [ProcessMaturityAssessments] ALTER COLUMN [ChangeManagement] decimal(4,2) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var62 sysname;
    SELECT @var62 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessGroups]') AND [c].[name] = N'AggregatedDurationMinutes');
    IF @var62 IS NOT NULL EXEC(N'ALTER TABLE [ProcessGroups] DROP CONSTRAINT [' + @var62 + '];');
    ALTER TABLE [ProcessGroups] ALTER COLUMN [AggregatedDurationMinutes] decimal(12,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var63 sysname;
    SELECT @var63 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Processes]') AND [c].[name] = N'EstimatedDuration');
    IF @var63 IS NOT NULL EXEC(N'ALTER TABLE [Processes] DROP CONSTRAINT [' + @var63 + '];');
    ALTER TABLE [Processes] ALTER COLUMN [EstimatedDuration] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var64 sysname;
    SELECT @var64 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Processes]') AND [c].[name] = N'AggregatedDurationMinutes');
    IF @var64 IS NOT NULL EXEC(N'ALTER TABLE [Processes] DROP CONSTRAINT [' + @var64 + '];');
    ALTER TABLE [Processes] ALTER COLUMN [AggregatedDurationMinutes] decimal(12,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var65 sysname;
    SELECT @var65 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Processes]') AND [c].[name] = N'ActualDuration');
    IF @var65 IS NOT NULL EXEC(N'ALTER TABLE [Processes] DROP CONSTRAINT [' + @var65 + '];');
    ALTER TABLE [Processes] ALTER COLUMN [ActualDuration] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var66 sysname;
    SELECT @var66 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MeasurementReadings]') AND [c].[name] = N'Value');
    IF @var66 IS NOT NULL EXEC(N'ALTER TABLE [MeasurementReadings] DROP CONSTRAINT [' + @var66 + '];');
    ALTER TABLE [MeasurementReadings] ALTER COLUMN [Value] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var67 sysname;
    SELECT @var67 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceSchedules]') AND [c].[name] = N'EstimatedDurationHours');
    IF @var67 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceSchedules] DROP CONSTRAINT [' + @var67 + '];');
    ALTER TABLE [MaintenanceSchedules] ALTER COLUMN [EstimatedDurationHours] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var68 sysname;
    SELECT @var68 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceRecords]') AND [c].[name] = N'DurationHours');
    IF @var68 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceRecords] DROP CONSTRAINT [' + @var68 + '];');
    ALTER TABLE [MaintenanceRecords] ALTER COLUMN [DurationHours] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var69 sysname;
    SELECT @var69 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[MaintenanceRecords]') AND [c].[name] = N'DowntimeHours');
    IF @var69 IS NOT NULL EXEC(N'ALTER TABLE [MaintenanceRecords] DROP CONSTRAINT [' + @var69 + '];');
    ALTER TABLE [MaintenanceRecords] ALTER COLUMN [DowntimeHours] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var70 sysname;
    SELECT @var70 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[KPITrends]') AND [c].[name] = N'Value');
    IF @var70 IS NOT NULL EXEC(N'ALTER TABLE [KPITrends] DROP CONSTRAINT [' + @var70 + '];');
    ALTER TABLE [KPITrends] ALTER COLUMN [Value] decimal(18,4) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var71 sysname;
    SELECT @var71 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[KPITrends]') AND [c].[name] = N'TargetValue');
    IF @var71 IS NOT NULL EXEC(N'ALTER TABLE [KPITrends] DROP CONSTRAINT [' + @var71 + '];');
    ALTER TABLE [KPITrends] ALTER COLUMN [TargetValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var72 sysname;
    SELECT @var72 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ImprovementMeasurements]') AND [c].[name] = N'ToBeValue');
    IF @var72 IS NOT NULL EXEC(N'ALTER TABLE [ImprovementMeasurements] DROP CONSTRAINT [' + @var72 + '];');
    ALTER TABLE [ImprovementMeasurements] ALTER COLUMN [ToBeValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var73 sysname;
    SELECT @var73 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ImprovementMeasurements]') AND [c].[name] = N'TargetValue');
    IF @var73 IS NOT NULL EXEC(N'ALTER TABLE [ImprovementMeasurements] DROP CONSTRAINT [' + @var73 + '];');
    ALTER TABLE [ImprovementMeasurements] ALTER COLUMN [TargetValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var74 sysname;
    SELECT @var74 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ImprovementMeasurements]') AND [c].[name] = N'AsIsValue');
    IF @var74 IS NOT NULL EXEC(N'ALTER TABLE [ImprovementMeasurements] DROP CONSTRAINT [' + @var74 + '];');
    ALTER TABLE [ImprovementMeasurements] ALTER COLUMN [AsIsValue] decimal(18,4) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var75 sysname;
    SELECT @var75 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ImprovementInitiatives]') AND [c].[name] = N'EstimatedTimeSavings');
    IF @var75 IS NOT NULL EXEC(N'ALTER TABLE [ImprovementInitiatives] DROP CONSTRAINT [' + @var75 + '];');
    ALTER TABLE [ImprovementInitiatives] ALTER COLUMN [EstimatedTimeSavings] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var76 sysname;
    SELECT @var76 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ImprovementInitiatives]') AND [c].[name] = N'ActualTimeSavings');
    IF @var76 IS NOT NULL EXEC(N'ALTER TABLE [ImprovementInitiatives] DROP CONSTRAINT [' + @var76 + '];');
    ALTER TABLE [ImprovementInitiatives] ALTER COLUMN [ActualTimeSavings] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var77 sysname;
    SELECT @var77 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ImprovementBenefitsReviews]') AND [c].[name] = N'ActualTimeSaving');
    IF @var77 IS NOT NULL EXEC(N'ALTER TABLE [ImprovementBenefitsReviews] DROP CONSTRAINT [' + @var77 + '];');
    ALTER TABLE [ImprovementBenefitsReviews] ALTER COLUMN [ActualTimeSaving] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var78 sysname;
    SELECT @var78 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Categories]') AND [c].[name] = N'AggregatedDurationMinutes');
    IF @var78 IS NOT NULL EXEC(N'ALTER TABLE [Categories] DROP CONSTRAINT [' + @var78 + '];');
    ALTER TABLE [Categories] ALTER COLUMN [AggregatedDurationMinutes] decimal(12,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var79 sysname;
    SELECT @var79 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Assets]') AND [c].[name] = N'LandAreaSqm');
    IF @var79 IS NOT NULL EXEC(N'ALTER TABLE [Assets] DROP CONSTRAINT [' + @var79 + '];');
    ALTER TABLE [Assets] ALTER COLUMN [LandAreaSqm] decimal(12,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var80 sysname;
    SELECT @var80 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Assets]') AND [c].[name] = N'DepreciationRate');
    IF @var80 IS NOT NULL EXEC(N'ALTER TABLE [Assets] DROP CONSTRAINT [' + @var80 + '];');
    ALTER TABLE [Assets] ALTER COLUMN [DepreciationRate] decimal(5,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var81 sysname;
    SELECT @var81 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Assets]') AND [c].[name] = N'BuiltUpAreaSqm');
    IF @var81 IS NOT NULL EXEC(N'ALTER TABLE [Assets] DROP CONSTRAINT [' + @var81 + '];');
    ALTER TABLE [Assets] ALTER COLUMN [BuiltUpAreaSqm] decimal(12,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var82 sysname;
    SELECT @var82 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AssetCategories]') AND [c].[name] = N'DefaultDepreciationRate');
    IF @var82 IS NOT NULL EXEC(N'ALTER TABLE [AssetCategories] DROP CONSTRAINT [' + @var82 + '];');
    ALTER TABLE [AssetCategories] ALTER COLUMN [DefaultDepreciationRate] decimal(5,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var83 sysname;
    SELECT @var83 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Activities]') AND [c].[name] = N'EstimatedDuration');
    IF @var83 IS NOT NULL EXEC(N'ALTER TABLE [Activities] DROP CONSTRAINT [' + @var83 + '];');
    ALTER TABLE [Activities] ALTER COLUMN [EstimatedDuration] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var84 sysname;
    SELECT @var84 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Activities]') AND [c].[name] = N'AggregatedDurationMinutes');
    IF @var84 IS NOT NULL EXEC(N'ALTER TABLE [Activities] DROP CONSTRAINT [' + @var84 + '];');
    ALTER TABLE [Activities] ALTER COLUMN [AggregatedDurationMinutes] decimal(12,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    DECLARE @var85 sysname;
    SELECT @var85 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Activities]') AND [c].[name] = N'ActualDuration');
    IF @var85 IS NOT NULL EXEC(N'ALTER TABLE [Activities] DROP CONSTRAINT [' + @var85 + '];');
    ALTER TABLE [Activities] ALTER COLUMN [ActualDuration] decimal(8,2) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519160801_SetDecimalPrecisions'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519160801_SetDecimalPrecisions', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519180657_TightenCatalogAndCommentLengths'
)
BEGIN
    DECLARE @var86 sysname;
    SELECT @var86 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'TargetAudienceEn');
    IF @var86 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var86 + '];');
    ALTER TABLE [ServiceCatalogInfos] ALTER COLUMN [TargetAudienceEn] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519180657_TightenCatalogAndCommentLengths'
)
BEGIN
    DECLARE @var87 sysname;
    SELECT @var87 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'TargetAudienceAr');
    IF @var87 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var87 + '];');
    ALTER TABLE [ServiceCatalogInfos] ALTER COLUMN [TargetAudienceAr] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519180657_TightenCatalogAndCommentLengths'
)
BEGIN
    DECLARE @var88 sysname;
    SELECT @var88 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'ProcedureEn');
    IF @var88 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var88 + '];');
    ALTER TABLE [ServiceCatalogInfos] ALTER COLUMN [ProcedureEn] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519180657_TightenCatalogAndCommentLengths'
)
BEGIN
    DECLARE @var89 sysname;
    SELECT @var89 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'ProcedureAr');
    IF @var89 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var89 + '];');
    ALTER TABLE [ServiceCatalogInfos] ALTER COLUMN [ProcedureAr] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519180657_TightenCatalogAndCommentLengths'
)
BEGIN
    DECLARE @var90 sysname;
    SELECT @var90 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'PreConditionsEn');
    IF @var90 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var90 + '];');
    ALTER TABLE [ServiceCatalogInfos] ALTER COLUMN [PreConditionsEn] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519180657_TightenCatalogAndCommentLengths'
)
BEGIN
    DECLARE @var91 sysname;
    SELECT @var91 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'PreConditionsAr');
    IF @var91 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var91 + '];');
    ALTER TABLE [ServiceCatalogInfos] ALTER COLUMN [PreConditionsAr] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519180657_TightenCatalogAndCommentLengths'
)
BEGIN
    DECLARE @var92 sysname;
    SELECT @var92 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'PoliciesEn');
    IF @var92 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var92 + '];');
    ALTER TABLE [ServiceCatalogInfos] ALTER COLUMN [PoliciesEn] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519180657_TightenCatalogAndCommentLengths'
)
BEGIN
    DECLARE @var93 sysname;
    SELECT @var93 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ServiceCatalogInfos]') AND [c].[name] = N'PoliciesAr');
    IF @var93 IS NOT NULL EXEC(N'ALTER TABLE [ServiceCatalogInfos] DROP CONSTRAINT [' + @var93 + '];');
    ALTER TABLE [ServiceCatalogInfos] ALTER COLUMN [PoliciesAr] nvarchar(4000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519180657_TightenCatalogAndCommentLengths'
)
BEGIN
    DECLARE @var94 sysname;
    SELECT @var94 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChangeRequestComments]') AND [c].[name] = N'Comment');
    IF @var94 IS NOT NULL EXEC(N'ALTER TABLE [ChangeRequestComments] DROP CONSTRAINT [' + @var94 + '];');
    ALTER TABLE [ChangeRequestComments] ALTER COLUMN [Comment] nvarchar(4000) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519180657_TightenCatalogAndCommentLengths'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519180657_TightenCatalogAndCommentLengths', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN

                    UPDATE [RiskActionPlans]
                    SET    [Status] = CASE
                                        WHEN [Status] = N'Not Started' THEN N'NotStarted'
                                        WHEN [Status] = N'In Progress' THEN N'InProgress'
                                        WHEN [Status] = N'On Track'    THEN N'OnTrack'
                                        WHEN [Status] = N'At Risk'     THEN N'AtRisk'
                                        WHEN [Status] IS NULL OR LTRIM(RTRIM([Status])) = N'' THEN N'NotStarted'
                                        ELSE [Status]
                                      END;
                
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN
    DECLARE @var95 sysname;
    SELECT @var95 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[RiskActionPlans]') AND [c].[name] = N'Status');
    IF @var95 IS NOT NULL EXEC(N'ALTER TABLE [RiskActionPlans] DROP CONSTRAINT [' + @var95 + '];');
    ALTER TABLE [RiskActionPlans] ALTER COLUMN [Status] nvarchar(32) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN
    ALTER TABLE [RiskActionPlans] ADD [CreatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN
    ALTER TABLE [RiskActionPlans] ADD [CreatedById] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN
    ALTER TABLE [RiskActionPlans] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN
    ALTER TABLE [RiskActionPlans] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN
    ALTER TABLE [RiskActionPlans] ADD [UpdatedAt] datetime2 NOT NULL DEFAULT (GETUTCDATE());
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN
    ALTER TABLE [RiskActionPlans] ADD [UpdatedById] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN
    ALTER TABLE [RiskActionPlans] ADD [Version] int NOT NULL DEFAULT 1;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN
    CREATE INDEX [IX_RiskActionPlans_IsDeleted] ON [RiskActionPlans] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260519181313_TightenRiskActionPlanAudit'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260519181313_TightenRiskActionPlanAudit', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520073244_CompleteChangeRequestLifecycle'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [CancellationReason] nvarchar(1000) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520073244_CompleteChangeRequestLifecycle'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [CancelledAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520073244_CompleteChangeRequestLifecycle'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [CancelledById] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520073244_CompleteChangeRequestLifecycle'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [ImplementedById] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520073244_CompleteChangeRequestLifecycle'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [ReviewStartedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520073244_CompleteChangeRequestLifecycle'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [ReviewStartedById] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520073244_CompleteChangeRequestLifecycle'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260520073244_CompleteChangeRequestLifecycle', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520122626_BackfillActivityCodeZeroPadding'
)
BEGIN

    UPDATE Activities
    SET Code = LEFT(Code, LEN(Code) - CHARINDEX('.', REVERSE(Code))) + '.0' + RIGHT(Code, CHARINDEX('.', REVERSE(Code)) - 1)
    WHERE Code IS NOT NULL
      AND CHARINDEX('.', Code) > 0
      AND CHARINDEX('.', REVERSE(Code)) = 2
      AND RIGHT(Code, 1) LIKE '[0-9]';

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260520122626_BackfillActivityCodeZeroPadding'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260520122626_BackfillActivityCodeZeroPadding', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521091300_AddSponsorAndCrImprovementLinks'
)
BEGIN
    ALTER TABLE [ImprovementInitiatives] ADD [SponsorId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521091300_AddSponsorAndCrImprovementLinks'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD [ImprovementId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260521091300_AddSponsorAndCrImprovementLinks'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260521091300_AddSponsorAndCrImprovementLinks', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523072618_AddImportBatch'
)
BEGIN
    CREATE TABLE [ImportBatches] (
        [Id] nvarchar(450) NOT NULL,
        [Kind] nvarchar(64) NOT NULL,
        [FileName] nvarchar(260) NULL,
        [ImportedCount] int NOT NULL,
        [SkippedCount] int NOT NULL,
        [Manifest] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedByName] nvarchar(256) NULL,
        [IsReverted] bit NOT NULL,
        [RevertedAt] datetime2 NULL,
        [RevertedCount] int NOT NULL,
        CONSTRAINT [PK_ImportBatches] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523072618_AddImportBatch'
)
BEGIN
    CREATE INDEX [IX_ImportBatches_IsReverted_CreatedAt] ON [ImportBatches] ([IsReverted], [CreatedAt]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523072618_AddImportBatch'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523072618_AddImportBatch', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN

    DECLARE @ren nvarchar(max) = N'';
    ;WITH fk(tbl, col) AS (
        SELECT * FROM (VALUES
            ('Activities','OwningUnitId'),('ActivityRacis','OrganizationUnitId'),('Assets','AssignedToUnitId'),
            ('BpmnLanes','OrganizationUnitId'),('ChangeRequests','OwningUnitId'),('CustomerFeedbacks','AssignedToUnitId'),
            ('EnterpriseRisks','OrganizationUnitId'),('FeedbackCategories','DefaultAssignedToUnitId'),('ImprovementInitiatives','OwningUnitId'),
            ('Incidents','AssignedToUnitId'),('JobRoles','OrganizationUnitId'),('KpiDefinitions','OwningUnitId'),
            ('OrganizationUnitResponsibilities','OrganizationUnitId'),('Problems','AssignedToUnitId'),('Processes','OwningUnitId'),
            ('ProcessRacis','OrganizationUnitId'),('ProcessTasks','OwningUnitId'),('Services','OwningUnitId'),
            ('SLADefinitions','ResponsibleUnitId'),('StrategicObjectives','OwningUnitId'),('SystemDefinitions','OwningUnitId'),
            ('TaskRacis','OrganizationUnitId'),('WorkloadConfigs','OrganizationUnitId'),('WorkloadScenarios','OwningUnitId')
        ) v(tbl,col)
    ),
    idx AS (
        SELECT t.name AS tblName, i.name AS idxName,
               STRING_AGG(c.name, '_') WITHIN GROUP (ORDER BY ic.key_ordinal) AS keyCols
        FROM sys.indexes i
        JOIN sys.tables t ON t.object_id = i.object_id
        JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id AND ic.is_included_column = 0
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        WHERE i.is_primary_key = 0 AND i.is_unique_constraint = 0 AND i.name IS NOT NULL
          AND t.name IN (SELECT tbl FROM fk)
        GROUP BY t.name, i.name
    )
    SELECT @ren += N'EXEC sp_rename N''' + idx.tblName + '.' + idx.idxName + ''', N''IX_' + idx.tblName + '_' + idx.keyCols + ''', ''INDEX'';' + CHAR(10)
    FROM idx
    JOIN fk ON fk.tbl = idx.tblName
    WHERE CHARINDEX('_' + fk.col, '_' + idx.keyCols + '_') > 0
      AND idx.idxName <> 'IX_' + idx.tblName + '_' + idx.keyCols;
    IF @ren <> N'' EXEC sp_executesql @ren;

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN

    DECLARE @mk nvarchar(max) = N'';
    ;WITH fk(tbl, col) AS (
        SELECT * FROM (VALUES
            ('Activities','OwningUnitId'),('ActivityRacis','OrganizationUnitId'),('Assets','AssignedToUnitId'),
            ('BpmnLanes','OrganizationUnitId'),('ChangeRequests','OwningUnitId'),('CustomerFeedbacks','AssignedToUnitId'),
            ('EnterpriseRisks','OrganizationUnitId'),('FeedbackCategories','DefaultAssignedToUnitId'),('ImprovementInitiatives','OwningUnitId'),
            ('Incidents','AssignedToUnitId'),('JobRoles','OrganizationUnitId'),('KpiDefinitions','OwningUnitId'),
            ('OrganizationUnitResponsibilities','OrganizationUnitId'),('Problems','AssignedToUnitId'),('Processes','OwningUnitId'),
            ('ProcessRacis','OrganizationUnitId'),('ProcessTasks','OwningUnitId'),('Services','OwningUnitId'),
            ('SLADefinitions','ResponsibleUnitId'),('StrategicObjectives','OwningUnitId'),('SystemDefinitions','OwningUnitId'),
            ('TaskRacis','OrganizationUnitId'),('WorkloadConfigs','OrganizationUnitId'),('WorkloadScenarios','OwningUnitId')
        ) v(tbl,col)
    )
    SELECT @mk += N'CREATE INDEX [IX_' + tbl + '_' + col + '] ON [' + tbl + '] ([' + col + ']);' + CHAR(10)
    FROM fk
    WHERE fk.tbl <> 'KpiDefinitions'   -- plain scalar: the model has NO index on it
      AND NOT EXISTS (
        SELECT 1 FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
        WHERE t.name = fk.tbl AND i.name = 'IX_' + fk.tbl + '_' + fk.col
    );
    IF @mk <> N'' EXEC sp_executesql @mk;

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN

    DECLARE @dropFk nvarchar(max) = N'';
    SELECT @dropFk += N'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + N'].[' + OBJECT_NAME(parent_object_id) + N'] DROP CONSTRAINT [' + name + N'];' + CHAR(10)
    FROM sys.foreign_keys
    WHERE referenced_object_id = OBJECT_ID(N'[dbo].[OrganizationUnits]');
    IF @dropFk <> N'' EXEC sp_executesql @dropFk;

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP TABLE [OrganizationUnits];
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN

    UPDATE [Activities] SET [OwningUnitId] = NULL;
    UPDATE [Assets] SET [AssignedToUnitId] = NULL;
    UPDATE [BpmnLanes] SET [OrganizationUnitId] = NULL;
    UPDATE [ChangeRequests] SET [OwningUnitId] = NULL;
    UPDATE [CustomerFeedbacks] SET [AssignedToUnitId] = NULL;
    UPDATE [EnterpriseRisks] SET [OrganizationUnitId] = NULL;
    UPDATE [FeedbackCategories] SET [DefaultAssignedToUnitId] = NULL;
    UPDATE [ImprovementInitiatives] SET [OwningUnitId] = NULL;
    UPDATE [Incidents] SET [AssignedToUnitId] = NULL;
    UPDATE [JobRoles] SET [OrganizationUnitId] = NULL;
    UPDATE [KpiDefinitions] SET [OwningUnitId] = NULL;
    UPDATE [Problems] SET [AssignedToUnitId] = NULL;
    UPDATE [Processes] SET [OwningUnitId] = NULL;
    UPDATE [Services] SET [OwningUnitId] = NULL;
    UPDATE [SLADefinitions] SET [ResponsibleUnitId] = NULL;
    UPDATE [StrategicObjectives] SET [OwningUnitId] = NULL;
    UPDATE [SystemDefinitions] SET [OwningUnitId] = NULL;
    UPDATE [WorkloadConfigs] SET [OrganizationUnitId] = NULL;
    UPDATE [WorkloadScenarios] SET [OwningUnitId] = NULL;
    ALTER TABLE [ProcessTasks] ALTER COLUMN [OwningUnitId] nvarchar(450) NULL;
    UPDATE [ProcessTasks] SET [OwningUnitId] = NULL;
    DELETE FROM [ProcessRacis];
    DELETE FROM [ActivityRacis];
    DELETE FROM [TaskRacis];
    DELETE FROM [OrganizationUnitResponsibilities];

END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_WorkloadScenarios_FiscalYear_OwningUnitId] ON [WorkloadScenarios];
    DROP INDEX [IX_WorkloadScenarios_OwningUnitId] ON [WorkloadScenarios];
    DECLARE @var96 sysname;
    SELECT @var96 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadScenarios]') AND [c].[name] = N'OwningUnitId');
    IF @var96 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadScenarios] DROP CONSTRAINT [' + @var96 + '];');
    ALTER TABLE [WorkloadScenarios] ALTER COLUMN [OwningUnitId] int NULL;
    CREATE INDEX [IX_WorkloadScenarios_FiscalYear_OwningUnitId] ON [WorkloadScenarios] ([FiscalYear], [OwningUnitId]);
    CREATE INDEX [IX_WorkloadScenarios_OwningUnitId] ON [WorkloadScenarios] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_WorkloadConfigs_OrganizationUnitId] ON [WorkloadConfigs];
    DECLARE @var97 sysname;
    SELECT @var97 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[WorkloadConfigs]') AND [c].[name] = N'OrganizationUnitId');
    IF @var97 IS NOT NULL EXEC(N'ALTER TABLE [WorkloadConfigs] DROP CONSTRAINT [' + @var97 + '];');
    ALTER TABLE [WorkloadConfigs] ALTER COLUMN [OrganizationUnitId] int NULL;
    CREATE INDEX [IX_WorkloadConfigs_OrganizationUnitId] ON [WorkloadConfigs] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_TaskRacis_OrganizationUnitId] ON [TaskRacis];
    DECLARE @var98 sysname;
    SELECT @var98 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[TaskRacis]') AND [c].[name] = N'OrganizationUnitId');
    IF @var98 IS NOT NULL EXEC(N'ALTER TABLE [TaskRacis] DROP CONSTRAINT [' + @var98 + '];');
    ALTER TABLE [TaskRacis] ALTER COLUMN [OrganizationUnitId] int NOT NULL;
    CREATE INDEX [IX_TaskRacis_OrganizationUnitId] ON [TaskRacis] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_SystemDefinitions_OwningUnitId] ON [SystemDefinitions];
    DECLARE @var99 sysname;
    SELECT @var99 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SystemDefinitions]') AND [c].[name] = N'OwningUnitId');
    IF @var99 IS NOT NULL EXEC(N'ALTER TABLE [SystemDefinitions] DROP CONSTRAINT [' + @var99 + '];');
    ALTER TABLE [SystemDefinitions] ALTER COLUMN [OwningUnitId] int NULL;
    CREATE INDEX [IX_SystemDefinitions_OwningUnitId] ON [SystemDefinitions] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_StrategicObjectives_OwningUnitId] ON [StrategicObjectives];
    DECLARE @var100 sysname;
    SELECT @var100 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[StrategicObjectives]') AND [c].[name] = N'OwningUnitId');
    IF @var100 IS NOT NULL EXEC(N'ALTER TABLE [StrategicObjectives] DROP CONSTRAINT [' + @var100 + '];');
    ALTER TABLE [StrategicObjectives] ALTER COLUMN [OwningUnitId] int NULL;
    CREATE INDEX [IX_StrategicObjectives_OwningUnitId] ON [StrategicObjectives] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_SLADefinitions_ResponsibleUnitId] ON [SLADefinitions];
    DECLARE @var101 sysname;
    SELECT @var101 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[SLADefinitions]') AND [c].[name] = N'ResponsibleUnitId');
    IF @var101 IS NOT NULL EXEC(N'ALTER TABLE [SLADefinitions] DROP CONSTRAINT [' + @var101 + '];');
    ALTER TABLE [SLADefinitions] ALTER COLUMN [ResponsibleUnitId] int NULL;
    CREATE INDEX [IX_SLADefinitions_ResponsibleUnitId] ON [SLADefinitions] ([ResponsibleUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_Services_OwningUnitId] ON [Services];
    DECLARE @var102 sysname;
    SELECT @var102 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Services]') AND [c].[name] = N'OwningUnitId');
    IF @var102 IS NOT NULL EXEC(N'ALTER TABLE [Services] DROP CONSTRAINT [' + @var102 + '];');
    ALTER TABLE [Services] ALTER COLUMN [OwningUnitId] int NULL;
    CREATE INDEX [IX_Services_OwningUnitId] ON [Services] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_ProcessTasks_OwningUnitId] ON [ProcessTasks];
    DECLARE @var103 sysname;
    SELECT @var103 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessTasks]') AND [c].[name] = N'OwningUnitId');
    IF @var103 IS NOT NULL EXEC(N'ALTER TABLE [ProcessTasks] DROP CONSTRAINT [' + @var103 + '];');
    ALTER TABLE [ProcessTasks] ALTER COLUMN [OwningUnitId] int NULL;
    CREATE INDEX [IX_ProcessTasks_OwningUnitId] ON [ProcessTasks] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_ProcessRacis_OrganizationUnitId] ON [ProcessRacis];
    DECLARE @var104 sysname;
    SELECT @var104 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ProcessRacis]') AND [c].[name] = N'OrganizationUnitId');
    IF @var104 IS NOT NULL EXEC(N'ALTER TABLE [ProcessRacis] DROP CONSTRAINT [' + @var104 + '];');
    ALTER TABLE [ProcessRacis] ALTER COLUMN [OrganizationUnitId] int NOT NULL;
    CREATE INDEX [IX_ProcessRacis_OrganizationUnitId] ON [ProcessRacis] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_Processes_OwningUnitId] ON [Processes];
    DECLARE @var105 sysname;
    SELECT @var105 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Processes]') AND [c].[name] = N'OwningUnitId');
    IF @var105 IS NOT NULL EXEC(N'ALTER TABLE [Processes] DROP CONSTRAINT [' + @var105 + '];');
    ALTER TABLE [Processes] ALTER COLUMN [OwningUnitId] int NULL;
    CREATE INDEX [IX_Processes_OwningUnitId] ON [Processes] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_Problems_AssignedToUnitId] ON [Problems];
    DECLARE @var106 sysname;
    SELECT @var106 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Problems]') AND [c].[name] = N'AssignedToUnitId');
    IF @var106 IS NOT NULL EXEC(N'ALTER TABLE [Problems] DROP CONSTRAINT [' + @var106 + '];');
    ALTER TABLE [Problems] ALTER COLUMN [AssignedToUnitId] int NULL;
    CREATE INDEX [IX_Problems_AssignedToUnitId] ON [Problems] ([AssignedToUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_OrganizationUnitResponsibilities_OrganizationUnitId] ON [OrganizationUnitResponsibilities];
    DECLARE @var107 sysname;
    SELECT @var107 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[OrganizationUnitResponsibilities]') AND [c].[name] = N'OrganizationUnitId');
    IF @var107 IS NOT NULL EXEC(N'ALTER TABLE [OrganizationUnitResponsibilities] DROP CONSTRAINT [' + @var107 + '];');
    ALTER TABLE [OrganizationUnitResponsibilities] ALTER COLUMN [OrganizationUnitId] int NOT NULL;
    CREATE INDEX [IX_OrganizationUnitResponsibilities_OrganizationUnitId] ON [OrganizationUnitResponsibilities] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DECLARE @var108 sysname;
    SELECT @var108 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[organization_units]') AND [c].[name] = N'update_by');
    IF @var108 IS NOT NULL EXEC(N'ALTER TABLE [organization_units] DROP CONSTRAINT [' + @var108 + '];');
    ALTER TABLE [organization_units] ALTER COLUMN [update_by] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DECLARE @var109 sysname;
    SELECT @var109 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[organization_units]') AND [c].[name] = N'unit_type');
    IF @var109 IS NOT NULL EXEC(N'ALTER TABLE [organization_units] DROP CONSTRAINT [' + @var109 + '];');
    ALTER TABLE [organization_units] ALTER COLUMN [unit_type] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DECLARE @var110 sysname;
    SELECT @var110 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[organization_units]') AND [c].[name] = N'unit_name_ar');
    IF @var110 IS NOT NULL EXEC(N'ALTER TABLE [organization_units] DROP CONSTRAINT [' + @var110 + '];');
    ALTER TABLE [organization_units] ALTER COLUMN [unit_name_ar] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DECLARE @var111 sysname;
    SELECT @var111 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[organization_units]') AND [c].[name] = N'unit_name');
    IF @var111 IS NOT NULL EXEC(N'ALTER TABLE [organization_units] DROP CONSTRAINT [' + @var111 + '];');
    ALTER TABLE [organization_units] ALTER COLUMN [unit_name] nvarchar(max) NOT NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DECLARE @var112 sysname;
    SELECT @var112 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[organization_units]') AND [c].[name] = N'created_by');
    IF @var112 IS NOT NULL EXEC(N'ALTER TABLE [organization_units] DROP CONSTRAINT [' + @var112 + '];');
    ALTER TABLE [organization_units] ALTER COLUMN [created_by] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [Code] nvarchar(450) NOT NULL DEFAULT N'';
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [DeletedAt] datetime2 NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [DescriptionAr] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [DescriptionEn] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [DisplayOrder] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [Email] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [HeadUserId] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [Level] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [Phone] nvarchar(max) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [organization_units] ADD [Version] int NOT NULL DEFAULT 0;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DECLARE @var113 sysname;
    SELECT @var113 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[KpiDefinitions]') AND [c].[name] = N'OwningUnitId');
    IF @var113 IS NOT NULL EXEC(N'ALTER TABLE [KpiDefinitions] DROP CONSTRAINT [' + @var113 + '];');
    ALTER TABLE [KpiDefinitions] ALTER COLUMN [OwningUnitId] int NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_JobRoles_OrganizationUnitId] ON [JobRoles];
    DECLARE @var114 sysname;
    SELECT @var114 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[JobRoles]') AND [c].[name] = N'OrganizationUnitId');
    IF @var114 IS NOT NULL EXEC(N'ALTER TABLE [JobRoles] DROP CONSTRAINT [' + @var114 + '];');
    ALTER TABLE [JobRoles] ALTER COLUMN [OrganizationUnitId] int NULL;
    CREATE INDEX [IX_JobRoles_OrganizationUnitId] ON [JobRoles] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_Incidents_AssignedToUnitId] ON [Incidents];
    DECLARE @var115 sysname;
    SELECT @var115 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Incidents]') AND [c].[name] = N'AssignedToUnitId');
    IF @var115 IS NOT NULL EXEC(N'ALTER TABLE [Incidents] DROP CONSTRAINT [' + @var115 + '];');
    ALTER TABLE [Incidents] ALTER COLUMN [AssignedToUnitId] int NULL;
    CREATE INDEX [IX_Incidents_AssignedToUnitId] ON [Incidents] ([AssignedToUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_ImprovementInitiatives_OwningUnitId] ON [ImprovementInitiatives];
    DECLARE @var116 sysname;
    SELECT @var116 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ImprovementInitiatives]') AND [c].[name] = N'OwningUnitId');
    IF @var116 IS NOT NULL EXEC(N'ALTER TABLE [ImprovementInitiatives] DROP CONSTRAINT [' + @var116 + '];');
    ALTER TABLE [ImprovementInitiatives] ALTER COLUMN [OwningUnitId] int NULL;
    CREATE INDEX [IX_ImprovementInitiatives_OwningUnitId] ON [ImprovementInitiatives] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_FeedbackCategories_DefaultAssignedToUnitId] ON [FeedbackCategories];
    DECLARE @var117 sysname;
    SELECT @var117 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[FeedbackCategories]') AND [c].[name] = N'DefaultAssignedToUnitId');
    IF @var117 IS NOT NULL EXEC(N'ALTER TABLE [FeedbackCategories] DROP CONSTRAINT [' + @var117 + '];');
    ALTER TABLE [FeedbackCategories] ALTER COLUMN [DefaultAssignedToUnitId] int NULL;
    CREATE INDEX [IX_FeedbackCategories_DefaultAssignedToUnitId] ON [FeedbackCategories] ([DefaultAssignedToUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_EnterpriseRisks_OrganizationUnitId] ON [EnterpriseRisks];
    DECLARE @var118 sysname;
    SELECT @var118 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[EnterpriseRisks]') AND [c].[name] = N'OrganizationUnitId');
    IF @var118 IS NOT NULL EXEC(N'ALTER TABLE [EnterpriseRisks] DROP CONSTRAINT [' + @var118 + '];');
    ALTER TABLE [EnterpriseRisks] ALTER COLUMN [OrganizationUnitId] int NULL;
    CREATE INDEX [IX_EnterpriseRisks_OrganizationUnitId] ON [EnterpriseRisks] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_CustomerFeedbacks_AssignedToUnitId] ON [CustomerFeedbacks];
    DECLARE @var119 sysname;
    SELECT @var119 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[CustomerFeedbacks]') AND [c].[name] = N'AssignedToUnitId');
    IF @var119 IS NOT NULL EXEC(N'ALTER TABLE [CustomerFeedbacks] DROP CONSTRAINT [' + @var119 + '];');
    ALTER TABLE [CustomerFeedbacks] ALTER COLUMN [AssignedToUnitId] int NULL;
    CREATE INDEX [IX_CustomerFeedbacks_AssignedToUnitId] ON [CustomerFeedbacks] ([AssignedToUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_ChangeRequests_OwningUnitId] ON [ChangeRequests];
    DECLARE @var120 sysname;
    SELECT @var120 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ChangeRequests]') AND [c].[name] = N'OwningUnitId');
    IF @var120 IS NOT NULL EXEC(N'ALTER TABLE [ChangeRequests] DROP CONSTRAINT [' + @var120 + '];');
    ALTER TABLE [ChangeRequests] ALTER COLUMN [OwningUnitId] int NULL;
    CREATE INDEX [IX_ChangeRequests_OwningUnitId] ON [ChangeRequests] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_BpmnLanes_OrganizationUnitId] ON [BpmnLanes];
    DECLARE @var121 sysname;
    SELECT @var121 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[BpmnLanes]') AND [c].[name] = N'OrganizationUnitId');
    IF @var121 IS NOT NULL EXEC(N'ALTER TABLE [BpmnLanes] DROP CONSTRAINT [' + @var121 + '];');
    ALTER TABLE [BpmnLanes] ALTER COLUMN [OrganizationUnitId] int NULL;
    CREATE INDEX [IX_BpmnLanes_OrganizationUnitId] ON [BpmnLanes] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_Assets_AssignedToUnitId] ON [Assets];
    DECLARE @var122 sysname;
    SELECT @var122 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Assets]') AND [c].[name] = N'AssignedToUnitId');
    IF @var122 IS NOT NULL EXEC(N'ALTER TABLE [Assets] DROP CONSTRAINT [' + @var122 + '];');
    ALTER TABLE [Assets] ALTER COLUMN [AssignedToUnitId] int NULL;
    CREATE INDEX [IX_Assets_AssignedToUnitId] ON [Assets] ([AssignedToUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_ActivityRacis_OrganizationUnitId] ON [ActivityRacis];
    DECLARE @var123 sysname;
    SELECT @var123 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[ActivityRacis]') AND [c].[name] = N'OrganizationUnitId');
    IF @var123 IS NOT NULL EXEC(N'ALTER TABLE [ActivityRacis] DROP CONSTRAINT [' + @var123 + '];');
    ALTER TABLE [ActivityRacis] ALTER COLUMN [OrganizationUnitId] int NOT NULL;
    CREATE INDEX [IX_ActivityRacis_OrganizationUnitId] ON [ActivityRacis] ([OrganizationUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    DROP INDEX [IX_Activities_OwningUnitId] ON [Activities];
    DECLARE @var124 sysname;
    SELECT @var124 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Activities]') AND [c].[name] = N'OwningUnitId');
    IF @var124 IS NOT NULL EXEC(N'ALTER TABLE [Activities] DROP CONSTRAINT [' + @var124 + '];');
    ALTER TABLE [Activities] ALTER COLUMN [OwningUnitId] int NULL;
    CREATE INDEX [IX_Activities_OwningUnitId] ON [Activities] ([OwningUnitId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    UPDATE [organization_units] SET [Code] = CONCAT('OU-', CAST([unit_id] AS varchar(10))) WHERE [Code] = '' OR [Code] IS NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    CREATE UNIQUE INDEX [IX_organization_units_Code] ON [organization_units] ([Code]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    CREATE INDEX [IX_organization_units_IsDeleted] ON [organization_units] ([IsDeleted]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [Activities] ADD CONSTRAINT [FK_Activities_organization_units_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [organization_units] ([unit_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [ActivityRacis] ADD CONSTRAINT [FK_ActivityRacis_organization_units_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [Assets] ADD CONSTRAINT [FK_Assets_organization_units_AssignedToUnitId] FOREIGN KEY ([AssignedToUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [BpmnLanes] ADD CONSTRAINT [FK_BpmnLanes_organization_units_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE NO ACTION;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [ChangeRequests] ADD CONSTRAINT [FK_ChangeRequests_organization_units_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [organization_units] ([unit_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [CustomerFeedbacks] ADD CONSTRAINT [FK_CustomerFeedbacks_organization_units_AssignedToUnitId] FOREIGN KEY ([AssignedToUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [EnterpriseRisks] ADD CONSTRAINT [FK_EnterpriseRisks_organization_units_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [FeedbackCategories] ADD CONSTRAINT [FK_FeedbackCategories_organization_units_DefaultAssignedToUnitId] FOREIGN KEY ([DefaultAssignedToUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [ImprovementInitiatives] ADD CONSTRAINT [FK_ImprovementInitiatives_organization_units_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [organization_units] ([unit_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [Incidents] ADD CONSTRAINT [FK_Incidents_organization_units_AssignedToUnitId] FOREIGN KEY ([AssignedToUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [JobRoles] ADD CONSTRAINT [FK_JobRoles_organization_units_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [OrganizationUnitResponsibilities] ADD CONSTRAINT [FK_OrganizationUnitResponsibilities_organization_units_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [Problems] ADD CONSTRAINT [FK_Problems_organization_units_AssignedToUnitId] FOREIGN KEY ([AssignedToUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [Processes] ADD CONSTRAINT [FK_Processes_organization_units_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [organization_units] ([unit_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [ProcessRacis] ADD CONSTRAINT [FK_ProcessRacis_organization_units_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [ProcessTasks] ADD CONSTRAINT [FK_ProcessTasks_organization_units_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [organization_units] ([unit_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [Services] ADD CONSTRAINT [FK_Services_organization_units_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [organization_units] ([unit_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [SLADefinitions] ADD CONSTRAINT [FK_SLADefinitions_organization_units_ResponsibleUnitId] FOREIGN KEY ([ResponsibleUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [StrategicObjectives] ADD CONSTRAINT [FK_StrategicObjectives_organization_units_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [organization_units] ([unit_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [SystemDefinitions] ADD CONSTRAINT [FK_SystemDefinitions_organization_units_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [organization_units] ([unit_id]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [TaskRacis] ADD CONSTRAINT [FK_TaskRacis_organization_units_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE CASCADE;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [WorkloadConfigs] ADD CONSTRAINT [FK_WorkloadConfigs_organization_units_OrganizationUnitId] FOREIGN KEY ([OrganizationUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    ALTER TABLE [WorkloadScenarios] ADD CONSTRAINT [FK_WorkloadScenarios_organization_units_OwningUnitId] FOREIGN KEY ([OwningUnitId]) REFERENCES [organization_units] ([unit_id]) ON DELETE SET NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523100616_MergeOrganizationUnitsToIntKey'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523100616_MergeOrganizationUnitsToIntKey', N'9.0.0');
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523194017_AddUserSecurityStamp'
)
BEGIN
    ALTER TABLE [user] ADD [security_stamp] nvarchar(64) NULL;
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260523194017_AddUserSecurityStamp'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260523194017_AddUserSecurityStamp', N'9.0.0');
END;

COMMIT;
GO

