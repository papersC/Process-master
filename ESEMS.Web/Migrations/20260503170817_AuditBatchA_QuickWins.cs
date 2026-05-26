using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AuditBatchA_QuickWins : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ────────────────────────────────────────────────────────────────
            // Idempotent adds for snapshot-drift columns. These already exist
            // in production (added at boot by Program.cs's ensure-columns SQL)
            // but were never recorded in the EF snapshot. We use IF COL_LENGTH
            // checks so this migration is safe against either state.
            // ────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(
                "IF COL_LENGTH('ProcessGroups','LegacyCode') IS NULL " +
                "ALTER TABLE [ProcessGroups] ADD [LegacyCode] nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('ProcessGroups','SortKey') IS NULL " +
                "ALTER TABLE [ProcessGroups] ADD [SortKey] nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('Processes','LegacyCode') IS NULL " +
                "ALTER TABLE [Processes] ADD [LegacyCode] nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('Processes','ParentProcessId') IS NULL " +
                "ALTER TABLE [Processes] ADD [ParentProcessId] nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('Processes','SortKey') IS NULL " +
                "ALTER TABLE [Processes] ADD [SortKey] nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('Categories','LegacyCode') IS NULL " +
                "ALTER TABLE [Categories] ADD [LegacyCode] nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('Categories','SortKey') IS NULL " +
                "ALTER TABLE [Categories] ADD [SortKey] nvarchar(max) NULL;");

            // ────────────────────────────────────────────────────────────────
            // Audit #14: ImprovementTeamMember.UserId int FK to user.user_id.
            // Existing rows must already have numeric strings — if any row
            // contains a non-numeric value the ALTER will fail loudly, which
            // is the right outcome (better than silently dropping the row).
            // ────────────────────────────────────────────────────────────────
            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "ImprovementTeamMembers",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // ────────────────────────────────────────────────────────────────
            // Audit #19: ImprovementMeasurement.Direction enum-as-string.
            // Column is added at app boot on live DBs; fresh DBs need ADD first.
            // ────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(
                "IF COL_LENGTH('ImprovementMeasurements','Direction') IS NULL " +
                "ALTER TABLE [ImprovementMeasurements] ADD [Direction] nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "ALTER TABLE [ImprovementMeasurements] ALTER COLUMN [Direction] nvarchar(20) NULL;");

            // ────────────────────────────────────────────────────────────────
            // Audit #13: replace the magic-string AppliesTo with two FK columns.
            // Add the new columns first, backfill from the parsed prefix, then
            // drop the legacy column. Do NOT drop AppliesTo before the backfill
            // or we lose data — the EF-scaffolded order was wrong.
            // ────────────────────────────────────────────────────────────────
            migrationBuilder.AddColumn<string>(
                name: "AppliesToProcessId",
                table: "ImprovementMeasurements",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppliesToServiceId",
                table: "ImprovementMeasurements",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.Sql(@"
                UPDATE ImprovementMeasurements
                SET AppliesToProcessId = SUBSTRING(AppliesTo, 9, 4000)
                WHERE AppliesTo LIKE 'process:%';

                UPDATE ImprovementMeasurements
                SET AppliesToServiceId = SUBSTRING(AppliesTo, 9, 4000)
                WHERE AppliesTo LIKE 'service:%';
            ");

            migrationBuilder.DropColumn(
                name: "AppliesTo",
                table: "ImprovementMeasurements");

            // ────────────────────────────────────────────────────────────────
            // Audit #4 / #11: status enums persisted as nvarchar(50). Existing
            // string rows ("Proposed", "InProgress", ...) round-trip unchanged.
            //
            // EF's AlterColumn assumes specific oldType values from the model
            // snapshot (nvarchar(450) for Initiatives, nvarchar(max) for
            // Actions). The live DB drifted in the opposite direction —
            // Status is actually nvarchar(MAX) on Initiatives too — so EF's
            // DROP INDEX / ALTER / RECREATE INDEX dance fails because MAX
            // columns can't be indexed.
            //
            // Bypass EF's AlterColumn entirely and use raw SQL that's safe
            // regardless of the starting type: drop any existing index, shrink
            // the column, recreate the index. Idempotent against drift.
            // ────────────────────────────────────────────────────────────────
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes
                           WHERE name = 'IX_ImprovementInitiatives_Status'
                             AND object_id = OBJECT_ID('ImprovementInitiatives'))
                    DROP INDEX [IX_ImprovementInitiatives_Status] ON [ImprovementInitiatives];

                ALTER TABLE [ImprovementInitiatives] ALTER COLUMN [Status] nvarchar(50) NOT NULL;

                CREATE INDEX [IX_ImprovementInitiatives_Status]
                    ON [ImprovementInitiatives]([Status]);

                ALTER TABLE [ImprovementActions] ALTER COLUMN [Status] nvarchar(50) NOT NULL;
            ");

            // Normalise legacy string-only Status values that were never in
            // the formal state machine (e.g. "Identified" / "Implemented" /
            // "In Progress") so the new enum value-converter can read them.
            migrationBuilder.Sql(@"
                UPDATE ImprovementInitiatives SET Status = 'Proposed'  WHERE Status = 'Identified';
                UPDATE ImprovementInitiatives SET Status = 'Completed' WHERE Status = 'Implemented';
                UPDATE ImprovementActions     SET Status = 'InProgress' WHERE Status = 'In Progress';
            ");

            migrationBuilder.CreateTable(
                name: "PrioritizationConfigs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    FiscalYear = table.Column<int>(type: "int", nullable: true),
                    ImpactCutoff = table.Column<int>(type: "int", nullable: false),
                    EffortCutoff = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrioritizationConfigs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementTeamMembers_UserId",
                table: "ImprovementTeamMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementMeasurements_AppliesToProcessId",
                table: "ImprovementMeasurements",
                column: "AppliesToProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementMeasurements_AppliesToServiceId",
                table: "ImprovementMeasurements",
                column: "AppliesToServiceId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImprovementMeasurements_Processes_AppliesToProcessId",
                table: "ImprovementMeasurements",
                column: "AppliesToProcessId",
                principalTable: "Processes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ImprovementMeasurements_Services_AppliesToServiceId",
                table: "ImprovementMeasurements",
                column: "AppliesToServiceId",
                principalTable: "Services",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ImprovementTeamMembers_user_UserId",
                table: "ImprovementTeamMembers",
                column: "UserId",
                principalTable: "user",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImprovementMeasurements_Processes_AppliesToProcessId",
                table: "ImprovementMeasurements");

            migrationBuilder.DropForeignKey(
                name: "FK_ImprovementMeasurements_Services_AppliesToServiceId",
                table: "ImprovementMeasurements");

            migrationBuilder.DropForeignKey(
                name: "FK_ImprovementTeamMembers_user_UserId",
                table: "ImprovementTeamMembers");

            migrationBuilder.DropTable(
                name: "PrioritizationConfigs");

            migrationBuilder.DropIndex(
                name: "IX_ImprovementTeamMembers_UserId",
                table: "ImprovementTeamMembers");

            migrationBuilder.DropIndex(
                name: "IX_ImprovementMeasurements_AppliesToProcessId",
                table: "ImprovementMeasurements");

            migrationBuilder.DropIndex(
                name: "IX_ImprovementMeasurements_AppliesToServiceId",
                table: "ImprovementMeasurements");

            migrationBuilder.DropColumn(
                name: "LegacyCode",
                table: "ProcessGroups");

            migrationBuilder.DropColumn(
                name: "SortKey",
                table: "ProcessGroups");

            migrationBuilder.DropColumn(
                name: "LegacyCode",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "ParentProcessId",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "SortKey",
                table: "Processes");

            migrationBuilder.DropColumn(
                name: "AppliesToProcessId",
                table: "ImprovementMeasurements");

            migrationBuilder.DropColumn(
                name: "AppliesToServiceId",
                table: "ImprovementMeasurements");

            migrationBuilder.DropColumn(
                name: "LegacyCode",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "SortKey",
                table: "Categories");

            migrationBuilder.AlterColumn<string>(
                name: "UserId",
                table: "ImprovementTeamMembers",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "Direction",
                table: "ImprovementMeasurements",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(20)",
                oldMaxLength: 20,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AppliesTo",
                table: "ImprovementMeasurements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ImprovementInitiatives",
                type: "nvarchar(450)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "ImprovementActions",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
