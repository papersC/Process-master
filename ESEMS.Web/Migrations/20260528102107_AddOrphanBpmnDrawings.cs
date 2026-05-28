using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddOrphanBpmnDrawings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrphanBpmnDrawings",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    DetectedName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    FilePrefix = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    BpmnXml = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    XmlSizeBytes = table.Column<int>(type: "int", nullable: false),
                    BestMatchScore = table.Column<double>(type: "float", nullable: true),
                    BestMatchProcessId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    BestMatchProcessName = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true),
                    LinkedProcessId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    LinkedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LinkedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrphanBpmnDrawings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OrphanBpmnDrawings_Processes_LinkedProcessId",
                        column: x => x.LinkedProcessId,
                        principalTable: "Processes",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrphanBpmnDrawings_LinkedProcessId",
                table: "OrphanBpmnDrawings",
                column: "LinkedProcessId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OrphanBpmnDrawings");
        }
    }
}
