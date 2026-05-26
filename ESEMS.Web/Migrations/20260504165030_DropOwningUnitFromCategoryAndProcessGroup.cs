using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class DropOwningUnitFromCategoryAndProcessGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Categories_OrganizationUnits_OwningUnitId",
                table: "Categories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcessGroups_OrganizationUnits_OwningUnitId",
                table: "ProcessGroups");

            migrationBuilder.DropIndex(
                name: "IX_ProcessGroups_OwningUnitId",
                table: "ProcessGroups");

            migrationBuilder.DropIndex(
                name: "IX_Categories_OwningUnitId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "OwningUnitId",
                table: "ProcessGroups");

            migrationBuilder.DropColumn(
                name: "OwningUnitId",
                table: "Categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OwningUnitId",
                table: "ProcessGroups",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwningUnitId",
                table: "Categories",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProcessGroups_OwningUnitId",
                table: "ProcessGroups",
                column: "OwningUnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Categories_OwningUnitId",
                table: "Categories",
                column: "OwningUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_Categories_OrganizationUnits_OwningUnitId",
                table: "Categories",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProcessGroups_OrganizationUnits_OwningUnitId",
                table: "ProcessGroups",
                column: "OwningUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id");
        }
    }
}
