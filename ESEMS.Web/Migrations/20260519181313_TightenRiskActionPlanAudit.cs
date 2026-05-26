using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class TightenRiskActionPlanAudit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // First: data-fix existing Status strings to match the new enum
            // members. Without this the column ALTER below will leave rows
            // EF can't deserialize ("Not Started" → no enum member).
            migrationBuilder.Sql(@"
                UPDATE [RiskActionPlans]
                SET    [Status] = CASE
                                    WHEN [Status] = N'Not Started' THEN N'NotStarted'
                                    WHEN [Status] = N'In Progress' THEN N'InProgress'
                                    WHEN [Status] = N'On Track'    THEN N'OnTrack'
                                    WHEN [Status] = N'At Risk'     THEN N'AtRisk'
                                    WHEN [Status] IS NULL OR LTRIM(RTRIM([Status])) = N'' THEN N'NotStarted'
                                    ELSE [Status]
                                  END;
            ");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "RiskActionPlans",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            // Audit columns. Defaults use GETUTCDATE() so existing rows land
            // with a sane CreatedAt/UpdatedAt (the entity-level property
            // initializer is C#-side only; SQL needs a literal default).
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "RiskActionPlans",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<string>(
                name: "CreatedById",
                table: "RiskActionPlans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "RiskActionPlans",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "RiskActionPlans",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "RiskActionPlans",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<string>(
                name: "UpdatedById",
                table: "RiskActionPlans",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Version",
                table: "RiskActionPlans",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_RiskActionPlans_IsDeleted",
                table: "RiskActionPlans",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RiskActionPlans_IsDeleted",
                table: "RiskActionPlans");

            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "RiskActionPlans");

            migrationBuilder.DropColumn(
                name: "CreatedById",
                table: "RiskActionPlans");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "RiskActionPlans");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "RiskActionPlans");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "RiskActionPlans");

            migrationBuilder.DropColumn(
                name: "UpdatedById",
                table: "RiskActionPlans");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "RiskActionPlans");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "RiskActionPlans",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(32)",
                oldMaxLength: 32);
        }
    }
}
