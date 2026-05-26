using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class PromoteServiceCategoryToServiceTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Add the new columns on Services.
            migrationBuilder.AddColumn<string>(
                name: "CategoryAr",
                table: "Services",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryEn",
                table: "Services",
                type: "nvarchar(max)",
                nullable: true);

            // 2) Backfill from the catalog sidecar BEFORE dropping the source
            //    columns, so the single demo row I seeded (and any future
            //    rows imported between EF runs) keeps its Category value.
            migrationBuilder.Sql(@"
                UPDATE s
                SET    s.CategoryEn = ci.CategoryEn,
                       s.CategoryAr = ci.CategoryAr
                FROM   Services s
                INNER JOIN ServiceCatalogInfos ci ON ci.ServiceId = s.Id
                WHERE  ci.CategoryEn IS NOT NULL OR ci.CategoryAr IS NOT NULL;
            ");

            // 3) Now safe to drop the catalog columns.
            migrationBuilder.DropColumn(
                name: "CategoryAr",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "CategoryEn",
                table: "ServiceCatalogInfos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CategoryAr",
                table: "ServiceCatalogInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CategoryEn",
                table: "ServiceCatalogInfos",
                type: "nvarchar(max)",
                nullable: true);

            // Reverse backfill — copy Category back to the sidecar before
            // dropping the Service-side columns.
            migrationBuilder.Sql(@"
                UPDATE ci
                SET    ci.CategoryEn = s.CategoryEn,
                       ci.CategoryAr = s.CategoryAr
                FROM   ServiceCatalogInfos ci
                INNER JOIN Services s ON s.Id = ci.ServiceId
                WHERE  s.CategoryEn IS NOT NULL OR s.CategoryAr IS NOT NULL;
            ");

            migrationBuilder.DropColumn(
                name: "CategoryAr",
                table: "Services");

            migrationBuilder.DropColumn(
                name: "CategoryEn",
                table: "Services");
        }
    }
}
