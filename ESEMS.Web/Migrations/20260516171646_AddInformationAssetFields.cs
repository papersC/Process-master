using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddInformationAssetFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Classification",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DataCustodianUserId",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DataFormat",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DataOwnerUserId",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "RecordCount",
                table: "Assets",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegulatoryTags",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RetentionMonths",
                table: "Assets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StorageSystem",
                table: "Assets",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Assets_Classification",
                table: "Assets",
                column: "Classification");

            migrationBuilder.CreateIndex(
                name: "IX_Assets_DataOwnerUserId",
                table: "Assets",
                column: "DataOwnerUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Assets_Classification",
                table: "Assets");

            migrationBuilder.DropIndex(
                name: "IX_Assets_DataOwnerUserId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "Classification",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DataCustodianUserId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DataFormat",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "DataOwnerUserId",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "RecordCount",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "RegulatoryTags",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "RetentionMonths",
                table: "Assets");

            migrationBuilder.DropColumn(
                name: "StorageSystem",
                table: "Assets");
        }
    }
}
