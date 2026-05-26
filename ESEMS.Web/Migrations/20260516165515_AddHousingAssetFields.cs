using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddHousingAssetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Bedrooms",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BuiltUpAreaSqm",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ConstructionStatus",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "District",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Emirate",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Floors",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GpsLatitude",
                table: "Assets",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "GpsLongitude",
                table: "Assets",
                type: "decimal(9,6)",
                precision: 9,
                scale: 6,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LandAreaSqm",
                table: "Assets",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ParentProjectId",
                table: "Assets",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PlotNumber",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TitleDeedNumber",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Units",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ConstructionStatus",
                table: "Assets",
                column: "ConstructionStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_ParentProjectId",
                table: "Assets",
                column: "ParentProjectId");

            migrationBuilder.AddForeignKey(
                name: "FK_Assets_Assets_ParentProjectId",
                table: "Assets",
                column: "ParentProjectId",
                principalTable: "Assets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Assets_Assets_ParentProjectId",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_ConstructionStatus",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_ParentProjectId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Bedrooms",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "BuiltUpAreaSqm",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ConstructionStatus",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "District",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Emirate",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Floors",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "GpsLatitude",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "GpsLongitude",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "LandAreaSqm",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "ParentProjectId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "PlotNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "TitleDeedNumber",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Units",
                table: "Assets");
        }
    }
}
