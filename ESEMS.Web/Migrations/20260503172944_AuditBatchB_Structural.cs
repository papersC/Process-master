using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AuditBatchB_Structural : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBenefitTracked",
                table: "ImprovementMeasurements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "StrategicObjectiveId",
                table: "ImprovementInitiatives",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImprovementAssets",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ImprovementId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AssetId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    RelationshipType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprovementAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImprovementAssets_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImprovementAssets_ImprovementInitiatives_ImprovementId",
                        column: x => x.ImprovementId,
                        principalTable: "ImprovementInitiatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImprovementBenefitsReviews",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ImprovementId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Period = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ActualCostSaving = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    ActualTimeSaving = table.Column<decimal>(type: "decimal(18,2)", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ReviewedById = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ReviewedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignedOffById = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    SignedOffByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SignedOffAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprovementBenefitsReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImprovementBenefitsReviews_ImprovementInitiatives_ImprovementId",
                        column: x => x.ImprovementId,
                        principalTable: "ImprovementInitiatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImprovementChangeLogs",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ImprovementId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ChangedById = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangeReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImprovementChangeLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImprovementChangeLogs_ImprovementInitiatives_ImprovementId",
                        column: x => x.ImprovementId,
                        principalTable: "ImprovementInitiatives",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementInitiatives_StrategicObjectiveId",
                table: "ImprovementInitiatives",
                column: "StrategicObjectiveId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementAssets_AssetId",
                table: "ImprovementAssets",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementAssets_ImprovementId",
                table: "ImprovementAssets",
                column: "ImprovementId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementAssets_ImprovementId_AssetId",
                table: "ImprovementAssets",
                columns: new[] { "ImprovementId", "AssetId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementBenefitsReviews_DueDate",
                table: "ImprovementBenefitsReviews",
                column: "DueDate");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementBenefitsReviews_ImprovementId",
                table: "ImprovementBenefitsReviews",
                column: "ImprovementId");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementBenefitsReviews_ImprovementId_Period",
                table: "ImprovementBenefitsReviews",
                columns: new[] { "ImprovementId", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementChangeLogs_ChangedAt",
                table: "ImprovementChangeLogs",
                column: "ChangedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ImprovementChangeLogs_ImprovementId",
                table: "ImprovementChangeLogs",
                column: "ImprovementId");

            migrationBuilder.AddForeignKey(
                name: "FK_ImprovementInitiatives_StrategicObjectives_StrategicObjectiveId",
                table: "ImprovementInitiatives",
                column: "StrategicObjectiveId",
                principalTable: "StrategicObjectives",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ImprovementInitiatives_StrategicObjectives_StrategicObjectiveId",
                table: "ImprovementInitiatives");

            migrationBuilder.DropTable(
                name: "ImprovementAssets");

            migrationBuilder.DropTable(
                name: "ImprovementBenefitsReviews");

            migrationBuilder.DropTable(
                name: "ImprovementChangeLogs");

            migrationBuilder.DropIndex(
                name: "IX_ImprovementInitiatives_StrategicObjectiveId",
                table: "ImprovementInitiatives");

            migrationBuilder.DropColumn(
                name: "IsBenefitTracked",
                table: "ImprovementMeasurements");

            migrationBuilder.DropColumn(
                name: "StrategicObjectiveId",
                table: "ImprovementInitiatives");
        }
    }
}
