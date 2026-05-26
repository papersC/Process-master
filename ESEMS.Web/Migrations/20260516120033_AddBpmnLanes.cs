using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddBpmnLanes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BpmnLanes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProcessId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    BpmnId = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    OrganizationUnitId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    MatchMethod = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    MatchedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MatchedById = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    FlowNodeRefsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BpmnLanes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BpmnLanes_OrganizationUnits_OrganizationUnitId",
                        column: x => x.OrganizationUnitId,
                        principalTable: "OrganizationUnits",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BpmnLanes_Processes_ProcessId",
                        column: x => x.ProcessId,
                        principalTable: "Processes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BpmnLanes_OrganizationUnitId",
                table: "BpmnLanes",
                column: "OrganizationUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_BpmnLanes_ProcessId_BpmnId",
                table: "BpmnLanes",
                columns: new[] { "ProcessId", "BpmnId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BpmnLanes");
        }
    }
}
