using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddProcessAndServiceResponsibilityLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProcessResponsibilities",
                columns: table => new
                {
                    ProcessId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ResponsibilityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessResponsibilities", x => new { x.ProcessId, x.ResponsibilityId });
                    table.ForeignKey(
                        name: "FK_ProcessResponsibilities_OrganizationUnitResponsibilities_ResponsibilityId",
                        column: x => x.ResponsibilityId,
                        principalTable: "OrganizationUnitResponsibilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcessResponsibilities_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServiceResponsibilities",
                columns: table => new
                {
                    ServiceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ResponsibilityId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceResponsibilities", x => new { x.ServiceId, x.ResponsibilityId });
                    table.ForeignKey(
                        name: "FK_ServiceResponsibilities_OrganizationUnitResponsibilities_ResponsibilityId",
                        column: x => x.ResponsibilityId,
                        principalTable: "OrganizationUnitResponsibilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ServiceResponsibilities_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessResponsibilities_ResponsibilityId",
                table: "ProcessResponsibilities",
                column: "ResponsibilityId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceResponsibilities_ResponsibilityId",
                table: "ServiceResponsibilities",
                column: "ResponsibilityId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProcessResponsibilities");

            migrationBuilder.DropTable(
                name: "ServiceResponsibilities");
        }
    }
}
