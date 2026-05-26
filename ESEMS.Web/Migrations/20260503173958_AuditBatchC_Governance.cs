using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AuditBatchC_Governance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReviewCycles",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Cadence = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OwningCommittee = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    StartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReviewCycles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ImprovementReviewCycleAssignments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ImprovementId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReviewCycleId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    LastGeneratedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprovementReviewCycleAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImprovementReviewCycleAssignments_ImprovementInitiatives_ImprovementId",
                        column: x => x.ImprovementId,
                        principalTable: "ImprovementInitiatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImprovementReviewCycleAssignments_ReviewCycles_ReviewCycleId",
                        column: x => x.ReviewCycleId,
                        principalTable: "ReviewCycles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementReviewCycleAssignments_ImprovementId",
                table: "ImprovementReviewCycleAssignments",
                column: "ImprovementId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementReviewCycleAssignments_ImprovementId_ReviewCycleId",
                table: "ImprovementReviewCycleAssignments",
                columns: new[] { "ImprovementId", "ReviewCycleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementReviewCycleAssignments_ReviewCycleId",
                table: "ImprovementReviewCycleAssignments",
                column: "ReviewCycleId");

            migrationBuilder.CreateIndex(
                name: "IX_ReviewCycles_IsActive",
                table: "ReviewCycles",
                column: "IsActive");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImprovementReviewCycleAssignments");

            migrationBuilder.DropTable(
                name: "ReviewCycles");
        }
    }
}
