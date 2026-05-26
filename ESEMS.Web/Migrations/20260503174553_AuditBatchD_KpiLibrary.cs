using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AuditBatchD_KpiLibrary : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "KpiDefinitionId",
                table: "ImprovementMeasurements",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "KpiDefinitions",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DefaultType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    OwningUnitId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
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
                    table.PrimaryKey("PK_KpiDefinitions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementMeasurements_KpiDefinitionId",
                table: "ImprovementMeasurements",
                column: "KpiDefinitionId");

            migrationBuilder.CreateIndex(
                name: "IX_KpiDefinitions_Code",
                table: "KpiDefinitions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_KpiDefinitions_IsActive",
                table: "KpiDefinitions",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_KpiDefinitions_IsDeleted",
                table: "KpiDefinitions",
                column: "IsDeleted");

            migrationBuilder.AddForeignKey(
                name: "FK_ImprovementMeasurements_KpiDefinitions_KpiDefinitionId",
                table: "ImprovementMeasurements",
                column: "KpiDefinitionId",
                principalTable: "KpiDefinitions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImprovementMeasurements_KpiDefinitions_KpiDefinitionId",
                table: "ImprovementMeasurements");

            migrationBuilder.DropTable(
                name: "KpiDefinitions");

            migrationBuilder.DropIndex(
                name: "IX_ImprovementMeasurements_KpiDefinitionId",
                table: "ImprovementMeasurements");

            migrationBuilder.DropColumn(
                name: "KpiDefinitionId",
                table: "ImprovementMeasurements");
        }
    }
}
