using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRoles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "JobRoleId",
                table: "TaskRacis",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobRoleId",
                table: "ProcessRacis",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JobRoleId",
                table: "ActivityRacis",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "JobRoles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    Category = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsLeadership = table.Column<bool>(type: "bit", nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false),
                    NameEn = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DescriptionAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobRoles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskRacis_JobRoleId",
                table: "TaskRacis",
                column: "JobRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessRacis_JobRoleId",
                table: "ProcessRacis",
                column: "JobRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityRacis_JobRoleId",
                table: "ActivityRacis",
                column: "JobRoleId");

            migrationBuilder.CreateIndex(
                name: "IX_JobRoles_Code",
                table: "JobRoles",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_JobRoles_IsDeleted",
                table: "JobRoles",
                column: "IsDeleted");

            migrationBuilder.AddForeignKey(
                name: "FK_ActivityRacis_JobRoles_JobRoleId",
                table: "ActivityRacis",
                column: "JobRoleId",
                principalTable: "JobRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessRacis_JobRoles_JobRoleId",
                table: "ProcessRacis",
                column: "JobRoleId",
                principalTable: "JobRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskRacis_JobRoles_JobRoleId",
                table: "TaskRacis",
                column: "JobRoleId",
                principalTable: "JobRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ActivityRacis_JobRoles_JobRoleId",
                table: "ActivityRacis");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessRacis_JobRoles_JobRoleId",
                table: "ProcessRacis");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskRacis_JobRoles_JobRoleId",
                table: "TaskRacis");

            migrationBuilder.DropTable(
                name: "JobRoles");

            migrationBuilder.DropIndex(
                name: "IX_TaskRacis_JobRoleId",
                table: "TaskRacis");

            migrationBuilder.DropIndex(
                name: "IX_ProcessRacis_JobRoleId",
                table: "ProcessRacis");

            migrationBuilder.DropIndex(
                name: "IX_ActivityRacis_JobRoleId",
                table: "ActivityRacis");

            migrationBuilder.DropColumn(
                name: "JobRoleId",
                table: "TaskRacis");

            migrationBuilder.DropColumn(
                name: "JobRoleId",
                table: "ProcessRacis");

            migrationBuilder.DropColumn(
                name: "JobRoleId",
                table: "ActivityRacis");
        }
    }
}
