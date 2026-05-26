using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <summary>
    /// Backfills existing ImprovementMeasurements rows whose MeasuringPeriod
    /// is NULL or empty with "Monthly". The MeasurementReminderHostedService
    /// can't ping owners about due readings on a measurement with no
    /// cadence, so existing data created before the entity-level default
    /// landed would have been silently un-pingable. The companion entity
    /// change (ImprovementMeasurement.MeasuringPeriod = "Monthly") covers
    /// new rows; this migration covers the legacy ones.
    /// </summary>
    public partial class BackfillMeasuringPeriodMonthly : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "UPDATE [ImprovementMeasurements] " +
                "SET [MeasuringPeriod] = 'Monthly' " +
                "WHERE [MeasuringPeriod] IS NULL OR LTRIM(RTRIM([MeasuringPeriod])) = ''");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: we can't tell which rows were originally
            // null vs explicitly set to "Monthly", so reverting would either
            // wipe legitimate values or leave them. Leave the data alone.
        }
    }
}
