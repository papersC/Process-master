using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class MergeOrganizationUnitsToIntKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── DRIFT FIX: rename indexes to EF's canonical names ───────
            // The live DB has migration drift: indexes covering the org-unit FK
            // columns exist under non-canonical names (e.g. IX_WorkloadScenarios_
            // FiscalYear_Unit vs the model's IX_WorkloadScenarios_FiscalYear_
            // OwningUnitId). EF's AlterColumn below drops/recreates those indexes
            // BY canonical name, which fails on the drifted names. Rename every
            // drifted index that covers an org-unit FK column to the canonical
            // IX_<table>_<keycols> form so EF's own operations succeed.
            migrationBuilder.Sql(@"
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
");

            // ─── DRIFT FIX: create canonical FK indexes the live DB is missing ─
            // The model/snapshot expects a single-column index IX_<table>_<col>
            // on each org-unit FK column; some are absent in the live DB (drift
            // the other way). EF's AlterColumn drops them BY canonical name and
            // fails when absent. Create any missing ones so the drop succeeds.
            migrationBuilder.Sql(@"
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
");

            // Drop every FK that references the old GUID OrganizationUnits table.
            // Done dynamically (not by hard-coded constraint names) because the
            // live DB has migration drift — some FK names differ from the model's
            // canonical names, or were never created — and a hard-coded
            // DropForeignKey throws "is not a constraint" on those.
            migrationBuilder.Sql(@"
DECLARE @dropFk nvarchar(max) = N'';
SELECT @dropFk += N'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + N'].[' + OBJECT_NAME(parent_object_id) + N'] DROP CONSTRAINT [' + name + N'];' + CHAR(10)
FROM sys.foreign_keys
WHERE referenced_object_id = OBJECT_ID(N'[dbo].[OrganizationUnits]');
IF @dropFk <> N'' EXEC sp_executesql @dropFk;
");

            migrationBuilder.DropTable(
                name: "OrganizationUnits");

            // ─── ORG-UNIT MERGE RESET (A2) ───────────────────────────────
            // The old business OrganizationUnit used GUID string keys; the
            // merged organization_units uses int. Existing FK values are GUIDs
            // that cannot convert to int, so clear them before the type change.
            // Nullable FKs → set NULL; the RACI + responsibility link rows have
            // a required unit and are re-created after re-import, so delete them;
            // ProcessTasks keeps its rows but its (now nullable) unit is cleared.
            migrationBuilder.Sql(@"
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
");

            migrationBuilder.AlterColumn<int>(
                name: "OwningUnitId",
                table: "WorkloadScenarios",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationUnitId",
                table: "WorkloadConfigs",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationUnitId",
                table: "TaskRacis",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "OwningUnitId",
                table: "SystemDefinitions",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OwningUnitId",
                table: "StrategicObjectives",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "ResponsibleUnitId",
                table: "SLADefinitions",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OwningUnitId",
                table: "Services",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OwningUnitId",
                table: "ProcessTasks",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationUnitId",
                table: "ProcessRacis",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "OwningUnitId",
                table: "Processes",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AssignedToUnitId",
                table: "Problems",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationUnitId",
                table: "OrganizationUnitResponsibilities",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "update_by",
                table: "organization_units",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "unit_type",
                table: "organization_units",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "unit_name_ar",
                table: "organization_units",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<string>(
                name: "unit_name",
                table: "organization_units",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(150)",
                oldMaxLength: 150);

            migrationBuilder.AlterColumn<int>(
                name: "created_by",
                table: "organization_units",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "organization_units",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "organization_units",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionAr",
                table: "organization_units",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DescriptionEn",
                table: "organization_units",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DisplayOrder",
                table: "organization_units",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "organization_units",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HeadUserId",
                table: "organization_units",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "organization_units",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "organization_units",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "organization_units",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Phone",
                table: "organization_units",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "organization_units",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "OwningUnitId",
                table: "KpiDefinitions",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldMaxLength: 450,
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationUnitId",
                table: "JobRoles",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AssignedToUnitId",
                table: "Incidents",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OwningUnitId",
                table: "ImprovementInitiatives",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "DefaultAssignedToUnitId",
                table: "FeedbackCategories",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationUnitId",
                table: "EnterpriseRisks",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AssignedToUnitId",
                table: "CustomerFeedbacks",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OwningUnitId",
                table: "ChangeRequests",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationUnitId",
                table: "BpmnLanes",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "AssignedToUnitId",
                table: "Assets",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "OrganizationUnitId",
                table: "ActivityRacis",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)");

            migrationBuilder.AlterColumn<int>(
                name: "OwningUnitId",
                table: "Activities",
                type: "int",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(450)",
                oldNullable: true);

            // Existing organization_units rows (the client's departments) all got
            // Code = '' from the AddColumn default above, which would collide under
            // the unique index. Backfill a unique code per row from the unique
            // unit_id before creating the index.
            migrationBuilder.Sql(
                "UPDATE [organization_units] SET [Code] = CONCAT('OU-', CAST([unit_id] AS varchar(10))) WHERE [Code] = '' OR [Code] IS NULL;");

            migrationBuilder.CreateIndex(
                name: "IX_organization_units_Code",
                table: "organization_units",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_units_IsDeleted",
                table: "organization_units",
                column: "IsDeleted");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_organization_units_OwningUnitId",
                table: "Activities",
                column: "OwningUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityRacis_organization_units_OrganizationUnitId",
                table: "ActivityRacis",
                column: "OrganizationUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_organization_units_AssignedToUnitId",
                table: "Assets",
                column: "AssignedToUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BpmnLanes_organization_units_OrganizationUnitId",
                table: "BpmnLanes",
                column: "OrganizationUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChangeRequests_organization_units_OwningUnitId",
                table: "ChangeRequests",
                column: "OwningUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerFeedbacks_organization_units_AssignedToUnitId",
                table: "CustomerFeedbacks",
                column: "AssignedToUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EnterpriseRisks_organization_units_OrganizationUnitId",
                table: "EnterpriseRisks",
                column: "OrganizationUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbackCategories_organization_units_DefaultAssignedToUnitId",
                table: "FeedbackCategories",
                column: "DefaultAssignedToUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ImprovementInitiatives_organization_units_OwningUnitId",
                table: "ImprovementInitiatives",
                column: "OwningUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_organization_units_AssignedToUnitId",
                table: "Incidents",
                column: "AssignedToUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_JobRoles_organization_units_OrganizationUnitId",
                table: "JobRoles",
                column: "OrganizationUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationUnitResponsibilities_organization_units_OrganizationUnitId",
                table: "OrganizationUnitResponsibilities",
                column: "OrganizationUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Problems_organization_units_AssignedToUnitId",
                table: "Problems",
                column: "AssignedToUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_organization_units_OwningUnitId",
                table: "Processes",
                column: "OwningUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessRacis_organization_units_OrganizationUnitId",
                table: "ProcessRacis",
                column: "OrganizationUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessTasks_organization_units_OwningUnitId",
                table: "ProcessTasks",
                column: "OwningUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id");

            migrationBuilder.AddForeignKey(
                name: "FK_Services_organization_units_OwningUnitId",
                table: "Services",
                column: "OwningUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id");

            migrationBuilder.AddForeignKey(
                name: "FK_SLADefinitions_organization_units_ResponsibleUnitId",
                table: "SLADefinitions",
                column: "ResponsibleUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StrategicObjectives_organization_units_OwningUnitId",
                table: "StrategicObjectives",
                column: "OwningUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id");

            migrationBuilder.AddForeignKey(
                name: "FK_SystemDefinitions_organization_units_OwningUnitId",
                table: "SystemDefinitions",
                column: "OwningUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskRacis_organization_units_OrganizationUnitId",
                table: "TaskRacis",
                column: "OrganizationUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkloadConfigs_organization_units_OrganizationUnitId",
                table: "WorkloadConfigs",
                column: "OrganizationUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkloadScenarios_organization_units_OwningUnitId",
                table: "WorkloadScenarios",
                column: "OwningUnitId",
                principalTable: "organization_units",
                principalColumn: "unit_id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_organization_units_OwningUnitId",
                table: "Activities");

            migrationBuilder.DropForeignKey(
                name: "FK_ActivityRacis_organization_units_OrganizationUnitId",
                table: "ActivityRacis");

            migrationBuilder.DropForeignKey(
                name: "FK_Assets_organization_units_AssignedToUnitId",
                table: "Assets");

            migrationBuilder.DropForeignKey(
                name: "FK_BpmnLanes_organization_units_OrganizationUnitId",
                table: "BpmnLanes");

            migrationBuilder.DropForeignKey(
                name: "FK_ChangeRequests_organization_units_OwningUnitId",
                table: "ChangeRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_CustomerFeedbacks_organization_units_AssignedToUnitId",
                table: "CustomerFeedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_EnterpriseRisks_organization_units_OrganizationUnitId",
                table: "EnterpriseRisks");

            migrationBuilder.DropForeignKey(
                name: "FK_FeedbackCategories_organization_units_DefaultAssignedToUnitId",
                table: "FeedbackCategories");

            migrationBuilder.DropForeignKey(
                name: "FK_ImprovementInitiatives_organization_units_OwningUnitId",
                table: "ImprovementInitiatives");

            migrationBuilder.DropForeignKey(
                name: "FK_Incidents_organization_units_AssignedToUnitId",
                table: "Incidents");

            migrationBuilder.DropForeignKey(
                name: "FK_JobRoles_organization_units_OrganizationUnitId",
                table: "JobRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_OrganizationUnitResponsibilities_organization_units_OrganizationUnitId",
                table: "OrganizationUnitResponsibilities");

            migrationBuilder.DropForeignKey(
                name: "FK_Problems_organization_units_AssignedToUnitId",
                table: "Problems");

            migrationBuilder.DropForeignKey(
                name: "FK_Processes_organization_units_OwningUnitId",
                table: "Processes");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessRacis_organization_units_OrganizationUnitId",
                table: "ProcessRacis");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessTasks_organization_units_OwningUnitId",
                table: "ProcessTasks");

            migrationBuilder.DropForeignKey(
                name: "FK_Services_organization_units_OwningUnitId",
                table: "Services");

            migrationBuilder.DropForeignKey(
                name: "FK_SLADefinitions_organization_units_ResponsibleUnitId",
                table: "SLADefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_StrategicObjectives_organization_units_OwningUnitId",
                table: "StrategicObjectives");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemDefinitions_organization_units_OwningUnitId",
                table: "SystemDefinitions");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskRacis_organization_units_OrganizationUnitId",
                table: "TaskRacis");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkloadConfigs_organization_units_OrganizationUnitId",
                table: "WorkloadConfigs");

            migrationBuilder.DropForeignKey(
                name: "FK_WorkloadScenarios_organization_units_OwningUnitId",
                table: "WorkloadScenarios");

            migrationBuilder.DropIndex(
                name: "IX_organization_units_Code",
                table: "organization_units");

            migrationBuilder.DropIndex(
                name: "IX_organization_units_IsDeleted",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "DescriptionAr",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "DescriptionEn",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "DisplayOrder",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "Email",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "HeadUserId",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "Phone",
                table: "organization_units");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "organization_units");

            migrationBuilder.AlterColumn<string>(
                name: "OwningUnitId",
                table: "WorkloadScenarios",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationUnitId",
                table: "WorkloadConfigs",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationUnitId",
                table: "TaskRacis",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "OwningUnitId",
                table: "SystemDefinitions",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OwningUnitId",
                table: "StrategicObjectives",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ResponsibleUnitId",
                table: "SLADefinitions",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OwningUnitId",
                table: "Services",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OwningUnitId",
                table: "ProcessTasks",
                type: "nvarchar(450)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationUnitId",
                table: "ProcessRacis",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "OwningUnitId",
                table: "Processes",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssignedToUnitId",
                table: "Problems",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationUnitId",
                table: "OrganizationUnitResponsibilities",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<int>(
                name: "update_by",
                table: "organization_units",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "unit_type",
                table: "organization_units",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "unit_name_ar",
                table: "organization_units",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "unit_name",
                table: "organization_units",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "created_by",
                table: "organization_units",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OwningUnitId",
                table: "KpiDefinitions",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationUnitId",
                table: "JobRoles",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssignedToUnitId",
                table: "Incidents",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OwningUnitId",
                table: "ImprovementInitiatives",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultAssignedToUnitId",
                table: "FeedbackCategories",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationUnitId",
                table: "EnterpriseRisks",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssignedToUnitId",
                table: "CustomerFeedbacks",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OwningUnitId",
                table: "ChangeRequests",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationUnitId",
                table: "BpmnLanes",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "AssignedToUnitId",
                table: "Assets",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OrganizationUnitId",
                table: "ActivityRacis",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "OwningUnitId",
                table: "Activities",
                type: "nvarchar(450)",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "OrganizationUnits",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ParentId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DescriptionAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DescriptionEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    HeadUserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UnitType = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrganizationUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrganizationUnits_OrganizationUnits_ParentId",
                        column: x => x.ParentId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_Code",
                table: "OrganizationUnits",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_IsDeleted",
                table: "OrganizationUnits",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_OrganizationUnits_ParentId",
                table: "OrganizationUnits",
                column: "ParentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_OrganizationUnits_OwningUnitId",
                table: "Activities",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityRacis_OrganizationUnits_OrganizationUnitId",
                table: "ActivityRacis",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_OrganizationUnits_AssignedToUnitId",
                table: "Assets",
                column: "AssignedToUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_BpmnLanes_OrganizationUnits_OrganizationUnitId",
                table: "BpmnLanes",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChangeRequests_OrganizationUnits_OwningUnitId",
                table: "ChangeRequests",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_CustomerFeedbacks_OrganizationUnits_AssignedToUnitId",
                table: "CustomerFeedbacks",
                column: "AssignedToUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_EnterpriseRisks_OrganizationUnits_OrganizationUnitId",
                table: "EnterpriseRisks",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_FeedbackCategories_OrganizationUnits_DefaultAssignedToUnitId",
                table: "FeedbackCategories",
                column: "DefaultAssignedToUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ImprovementInitiatives_OrganizationUnits_OwningUnitId",
                table: "ImprovementInitiatives",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Incidents_OrganizationUnits_AssignedToUnitId",
                table: "Incidents",
                column: "AssignedToUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_JobRoles_OrganizationUnits_OrganizationUnitId",
                table: "JobRoles",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OrganizationUnitResponsibilities_OrganizationUnits_OrganizationUnitId",
                table: "OrganizationUnitResponsibilities",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Problems_OrganizationUnits_AssignedToUnitId",
                table: "Problems",
                column: "AssignedToUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Processes_OrganizationUnits_OwningUnitId",
                table: "Processes",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessRacis_OrganizationUnits_OrganizationUnitId",
                table: "ProcessRacis",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessTasks_OrganizationUnits_OwningUnitId",
                table: "ProcessTasks",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Services_OrganizationUnits_OwningUnitId",
                table: "Services",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SLADefinitions_OrganizationUnits_ResponsibleUnitId",
                table: "SLADefinitions",
                column: "ResponsibleUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_StrategicObjectives_OrganizationUnits_OwningUnitId",
                table: "StrategicObjectives",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_SystemDefinitions_OrganizationUnits_OwningUnitId",
                table: "SystemDefinitions",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskRacis_OrganizationUnits_OrganizationUnitId",
                table: "TaskRacis",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkloadConfigs_OrganizationUnits_OrganizationUnitId",
                table: "WorkloadConfigs",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_WorkloadScenarios_OrganizationUnits_OwningUnitId",
                table: "WorkloadScenarios",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
