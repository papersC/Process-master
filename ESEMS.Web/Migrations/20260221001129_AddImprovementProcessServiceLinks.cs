using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddImprovementProcessServiceLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImprovementProcesses",
                columns: table => new
                {
                    ImprovementId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProcessId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprovementProcesses", x => new { x.ImprovementId, x.ProcessId });
                    table.ForeignKey(
                        name: "FK_ImprovementProcesses_ImprovementInitiatives_ImprovementId",
                        column: x => x.ImprovementId,
                        principalTable: "ImprovementInitiatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImprovementProcesses_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImprovementServices",
                columns: table => new
                {
                    ImprovementId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ServiceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprovementServices", x => new { x.ImprovementId, x.ServiceId });
                    table.ForeignKey(
                        name: "FK_ImprovementServices_ImprovementInitiatives_ImprovementId",
                        column: x => x.ImprovementId,
                        principalTable: "ImprovementInitiatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImprovementServices_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementProcesses_ProcessId",
                table: "ImprovementProcesses",
                column: "ProcessId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementServices_ServiceId",
                table: "ImprovementServices",
                column: "ServiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImprovementProcesses");

            migrationBuilder.DropTable(
                name: "ImprovementServices");
        }
    }
}
