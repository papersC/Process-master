using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <summary>
    /// Atomically migrates every user who still holds a legacy CustomUserRole
    /// row over to a matching Plan X UserRoleGroup row, then drops the legacy
    /// tables. Performed in this order INSIDE the migration so that the data
    /// migration and the table drop commit in the same transaction — there is
    /// no window where a prod user is left with neither legacy nor Plan X
    /// permissions.
    ///
    /// Idempotent for every step (seed and backfill use NOT EXISTS guards), so
    /// re-running on a dev database that already has the target rows is a
    /// no-op for those rows.
    /// </summary>
    public partial class DropLegacyRoleTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Make sure the four parity RoleGroups exist before the
            //    backfill references them. The Program.cs seeder also creates
            //    these, but it runs AFTER migrations — so we can't rely on
            //    them being present here.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[RoleGroups] WHERE [Code] = 'administrator')
                BEGIN
                    INSERT INTO [dbo].[RoleGroups]
                        ([Id], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr],
                         [ScopeLevel], [Permissions], [Icon], [Color], [IsActive],
                         [IsSystemRole], [Code], [CreatedAt], [UpdatedAt])
                    VALUES
                        (NEWID(), N'Administrator', N'مدير النظام',
                         N'Full system access — every module, every action',
                         N'وصول كامل للنظام — جميع الوحدات وجميع الإجراءات',
                         N'All', N'*.*', N'shield', N'#005B99', 1, 1, 'administrator',
                         GETUTCDATE(), GETUTCDATE());
                END;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[RoleGroups] WHERE [Code] = 'editor')
                BEGIN
                    INSERT INTO [dbo].[RoleGroups]
                        ([Id], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr],
                         [ScopeLevel], [Permissions], [Icon], [Color], [IsActive],
                         [IsSystemRole], [Code], [CreatedAt], [UpdatedAt])
                    VALUES
                        (NEWID(), N'Editor', N'محرر',
                         N'Create/edit/delete across all modules (no approval)',
                         N'إنشاء وتعديل وحذف عبر جميع الوحدات (بدون اعتماد)',
                         N'All',
                         N'Improvement.View,Improvement.Create,Improvement.Edit,Improvement.Delete,Improvement.Export,Measurement.View,Measurement.Create,Measurement.Edit,Measurement.Delete,Measurement.Export,Process.View,Process.Create,Process.Edit,Process.Delete,Process.Export,Service.View,Service.Create,Service.Edit,Service.Delete,Service.Export,Risk.View,Risk.Create,Risk.Edit,Risk.Delete,Risk.Export,Asset.View,Asset.Create,Asset.Edit,Asset.Delete,Asset.Export,Incident.View,Incident.Create,Incident.Edit,Incident.Delete,Problem.View,Problem.Create,Problem.Edit,Problem.Delete,ChangeRequest.View,ChangeRequest.Create,ChangeRequest.Edit,ChangeRequest.Delete,WorkflowTask.View,WorkflowTask.Create,WorkflowTask.Edit,WorkflowTask.Delete,OrganizationUnit.View,OrganizationUnit.Create,OrganizationUnit.Edit,Workflow.View,Reports.View,Reports.Export,Ai.View',
                         N'edit-3', N'#005B99', 1, 1, 'editor', GETUTCDATE(), GETUTCDATE());
                END;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[RoleGroups] WHERE [Code] = 'approver')
                BEGIN
                    INSERT INTO [dbo].[RoleGroups]
                        ([Id], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr],
                         [ScopeLevel], [Permissions], [Icon], [Color], [IsActive],
                         [IsSystemRole], [Code], [CreatedAt], [UpdatedAt])
                    VALUES
                        (NEWID(), N'Approver', N'معتمد',
                         N'View + approve across approval-bearing modules',
                         N'عرض واعتماد عبر الوحدات التي تتطلب اعتماداً',
                         N'All',
                         N'Improvement.View,Improvement.Approve,Risk.View,Risk.Approve,Workflow.View,Workflow.Approve,ChangeRequest.View,ChangeRequest.Approve,WorkflowTask.View,Process.View,Service.View,Measurement.View,OrganizationUnit.View,Reports.View',
                         N'check-circle', N'#005B99', 1, 1, 'approver',
                         GETUTCDATE(), GETUTCDATE());
                END;
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM [dbo].[RoleGroups] WHERE [Code] = 'viewer')
                BEGIN
                    INSERT INTO [dbo].[RoleGroups]
                        ([Id], [NameEn], [NameAr], [DescriptionEn], [DescriptionAr],
                         [ScopeLevel], [Permissions], [Icon], [Color], [IsActive],
                         [IsSystemRole], [Code], [CreatedAt], [UpdatedAt])
                    VALUES
                        (NEWID(), N'Viewer', N'مشاهد',
                         N'Read-only access to every module',
                         N'وصول للقراءة فقط لجميع الوحدات',
                         N'All',
                         N'Improvement.View,Measurement.View,Process.View,Service.View,Risk.View,Asset.View,Incident.View,Problem.View,ChangeRequest.View,WorkflowTask.View,OrganizationUnit.View,Workflow.View,Reports.View,Users.View,Settings.View,Ai.View,Workload.View',
                         N'eye', N'#005B99', 1, 1, 'viewer', GETUTCDATE(), GETUTCDATE());
                END;
            ");

            // 2) Backfill: every user who still holds a legacy CustomUserRole
            //    row gets a matching Plan X UserRoleGroup row if they don't
            //    already have one. Guarded by OBJECT_ID so this migration is
            //    safe to re-run after a partial application.
            migrationBuilder.Sql(@"
                IF OBJECT_ID('dbo.user_roles', 'U') IS NOT NULL AND OBJECT_ID('dbo.roles', 'U') IS NOT NULL
                BEGIN
                    ;WITH legacy_pairs AS (
                        SELECT ur.[user_id] AS UserId,
                               CASE r.[role_name]
                                   WHEN 'ADMIN'    THEN 'administrator'
                                   WHEN 'EDITOR'   THEN 'editor'
                                   WHEN 'APPROVER' THEN 'approver'
                                   WHEN 'VIEWER'   THEN 'viewer'
                               END AS RgCode
                        FROM [dbo].[user_roles] ur
                        INNER JOIN [dbo].[roles] r ON r.[role_id] = ur.[role_id]
                    )
                    INSERT INTO [dbo].[UserRoleGroups] ([Id], [UserId], [RoleGroupId], [AssignedBy], [AssignedAt])
                    SELECT LOWER(CONVERT(NVARCHAR(36), NEWID())), lp.UserId, rg.[Id], 1, GETUTCDATE()
                    FROM legacy_pairs lp
                    INNER JOIN [dbo].[RoleGroups] rg ON rg.[Code] = lp.RgCode
                    WHERE lp.RgCode IS NOT NULL
                      AND NOT EXISTS (
                          SELECT 1 FROM [dbo].[UserRoleGroups] urg
                          WHERE urg.[UserId] = lp.UserId AND urg.[RoleGroupId] = rg.[Id]
                      );
                END;
            ");

            // 3) Now safe to drop the legacy tables.
            migrationBuilder.DropTable(
                name: "user_roles");

            migrationBuilder.DropTable(
                name: "roles");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    role_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    created_by = table.Column<int>(type: "int", nullable: false),
                    created_date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    role_name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    role_name_ar = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    update_by = table.Column<int>(type: "int", nullable: false),
                    update_date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.role_id);
                });

            migrationBuilder.CreateTable(
                name: "user_roles",
                columns: table => new
                {
                    user_role_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    role_id = table.Column<int>(type: "int", nullable: false),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    assigned_by = table.Column<int>(type: "int", nullable: false),
                    assigned_date = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_roles", x => x.user_role_id);
                    table.ForeignKey(
                        name: "FK_user_roles_roles_role_id",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "role_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_user_roles_user_user_id",
                        column: x => x.user_id,
                        principalTable: "user",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_roles_role_name",
                table: "roles",
                column: "role_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_role_id",
                table: "user_roles",
                column: "role_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_roles_user_id_role_id",
                table: "user_roles",
                columns: new[] { "user_id", "role_id" },
                unique: true);
        }
    }
}
