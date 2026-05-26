using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddJobRoleOrganizationUnitScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrganizationUnitId",
                table: "JobRoles",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobRoles_OrganizationUnitId",
                table: "JobRoles",
                column: "OrganizationUnitId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobRoles_OrganizationUnits_OrganizationUnitId",
                table: "JobRoles",
                column: "OrganizationUnitId",
                principalTable: "OrganizationUnits",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobRoles_OrganizationUnits_OrganizationUnitId",
                table: "JobRoles");

            migrationBuilder.DropIndex(
                name: "IX_JobRoles_OrganizationUnitId",
                table: "JobRoles");

            migrationBuilder.DropColumn(
                name: "OrganizationUnitId",
                table: "JobRoles");
        }
    }
}
