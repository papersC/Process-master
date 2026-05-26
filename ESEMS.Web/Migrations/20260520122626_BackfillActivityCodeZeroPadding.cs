using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <summary>
    /// F-PA-003 data-fix migration. Legacy Activity rows persisted with
    /// single-digit final segments (e.g. "1.2.3.5") because the standalone
    /// /Activities/Create form formatted the suffix as "{n}" instead of
    /// "{n:D2}". That was fixed in commit cba5cdf for new rows, but existing
    /// rows still render as "1.2.3.5" beside "1.2.3.05" in lists, which
    /// breaks lexicographic sort and looks inconsistent.
    ///
    /// This migration pads the final segment of every Activity.Code to two
    /// digits when it's a single digit. SQL Server-flavored — uses CHARINDEX
    /// + REVERSE so it works without CTEs/string_split.
    ///
    /// Idempotent: a row already in "x.y.z.NN" form has a 2+ digit final
    /// segment and won't be touched by the WHERE clause.
    /// Down() is intentionally empty — un-padding a zero-padded code would
    /// regress the bug; we never want to restore single-digit suffixes.
    /// </summary>
    public partial class BackfillActivityCodeZeroPadding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
UPDATE Activities
SET Code = LEFT(Code, LEN(Code) - CHARINDEX('.', REVERSE(Code))) + '.0' + RIGHT(Code, CHARINDEX('.', REVERSE(Code)) - 1)
WHERE Code IS NOT NULL
  AND CHARINDEX('.', Code) > 0
  AND CHARINDEX('.', REVERSE(Code)) = 2
  AND RIGHT(Code, 1) LIKE '[0-9]';
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty — we never want to un-pad codes.
        }
    }
}
