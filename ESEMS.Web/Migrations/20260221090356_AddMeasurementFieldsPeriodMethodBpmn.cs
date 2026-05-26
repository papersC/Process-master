using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddMeasurementFieldsPeriodMethodBpmn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AppliesTo",
                table: "ImprovementMeasurements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BpmnReference",
                table: "ImprovementMeasurements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeasuringMethod",
                table: "ImprovementMeasurements",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MeasuringPeriod",
                table: "ImprovementMeasurements",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AppliesTo",
                table: "ImprovementMeasurements");

            migrationBuilder.DropColumn(
                name: "BpmnReference",
                table: "ImprovementMeasurements");

            migrationBuilder.DropColumn(
                name: "MeasuringMethod",
                table: "ImprovementMeasurements");

            migrationBuilder.DropColumn(
                name: "MeasuringPeriod",
                table: "ImprovementMeasurements");
        }
    }
}
