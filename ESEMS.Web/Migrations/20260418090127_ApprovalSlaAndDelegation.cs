using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class ApprovalSlaAndDelegation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF COL_LENGTH('WorkflowSteps','DelegatedFromName') IS NULL " +
                "ALTER TABLE [WorkflowSteps] ADD [DelegatedFromName] nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('WorkflowSteps','DelegatedFromUserId') IS NULL " +
                "ALTER TABLE [WorkflowSteps] ADD [DelegatedFromUserId] int NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('WorkflowSteps','DelegationExpiresAt') IS NULL " +
                "ALTER TABLE [WorkflowSteps] ADD [DelegationExpiresAt] datetime2 NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('WorkflowSteps','DueAt') IS NULL " +
                "ALTER TABLE [WorkflowSteps] ADD [DueAt] datetime2 NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('WorkflowSteps','EscalatedAt') IS NULL " +
                "ALTER TABLE [WorkflowSteps] ADD [EscalatedAt] datetime2 NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('ApprovalConfigurations','EscalationUserId') IS NULL " +
                "ALTER TABLE [ApprovalConfigurations] ADD [EscalationUserId] int NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('ApprovalConfigurations','EscalationUserName') IS NULL " +
                "ALTER TABLE [ApprovalConfigurations] ADD [EscalationUserName] nvarchar(max) NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('ApprovalConfigurations','Level1SlaHours') IS NULL " +
                "ALTER TABLE [ApprovalConfigurations] ADD [Level1SlaHours] int NULL;");
            migrationBuilder.Sql(
                "IF COL_LENGTH('ApprovalConfigurations','Level2SlaHours') IS NULL " +
                "ALTER TABLE [ApprovalConfigurations] ADD [Level2SlaHours] int NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DelegatedFromName",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "DelegatedFromUserId",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "DelegationExpiresAt",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "DueAt",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "EscalatedAt",
                table: "WorkflowSteps");

            migrationBuilder.DropColumn(
                name: "EscalationUserId",
                table: "ApprovalConfigurations");

            migrationBuilder.DropColumn(
                name: "EscalationUserName",
                table: "ApprovalConfigurations");

            migrationBuilder.DropColumn(
                name: "Level1SlaHours",
                table: "ApprovalConfigurations");

            migrationBuilder.DropColumn(
                name: "Level2SlaHours",
                table: "ApprovalConfigurations");
        }
    }
}
