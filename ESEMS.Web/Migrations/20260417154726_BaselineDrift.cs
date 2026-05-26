using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <summary>
    /// Baseline-drift migration. The live ESEMS DB picked up ~20 new tables
    /// (Notifications, RoleGroups, WorkloadConfigs, UserDocuments, etc.) via
    /// manual SQL between Feb 2026 and Apr 2026 without matching EF
    /// migrations, so the ModelSnapshot fell badly out of sync.
    ///
    /// This migration's Up/Down are intentionally EMPTY — applying it does
    /// nothing to the schema, it just records an entry in __EFMigrationsHistory
    /// so EF's snapshot (regenerated automatically from the current DbContext)
    /// aligns with the live DB. Future `dotnet ef migrations add` calls now
    /// scaffold only the true delta against this baseline.
    ///
    /// Caveat: a brand-new dev DB built from scratch via `dotnet ef database
    /// update` will NOT get the drift tables — they were manual-SQL only and
    /// aren't captured here. For new environments, restore from a live DB
    /// backup first, or use `dbcontext scaffold` to re-baseline from that DB.
    /// </summary>
    public partial class BaselineDrift : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty - see class summary.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally empty - see class summary.
        }
    }
}
