using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddOrganizationUnitType : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "UnitType",
                table: "OrganizationUnits",
                type: "int",
                nullable: true);

            // Backfill existing rows: 0→Sector, 1→Department, 2→Section. Levels
            // 3 (Function) and 4 (SubFunction) stay NULL — they're ambiguous
            // between Center/Office/etc. and a human should pick per row.
            // Enum mapping: Sector=0, Department=1, Section=2 (matches Level).
            migrationBuilder.Sql(@"
                UPDATE [OrganizationUnits]
                SET [UnitType] = [Level]
                WHERE [Level] IN (0, 1, 2) AND [UnitType] IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnitType",
                table: "OrganizationUnits");
        }
    }
}
