using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddImportBatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOTE: EF also detected drift on user.is_active (User.IsActive exists
            // in the model but predates the migration snapshot; the column already
            // lives in the databases). Intentionally NOT emitted here — adding it
            // with defaultValue:false would deactivate every existing user and
            // break login. Left as a separate decision; this migration only adds
            // the import-undo table.
            migrationBuilder.CreateTable(
                name: "ImportBatches",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Kind = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: true),
                    ImportedCount = table.Column<int>(type: "int", nullable: false),
                    SkippedCount = table.Column<int>(type: "int", nullable: false),
                    Manifest = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    IsReverted = table.Column<bool>(type: "bit", nullable: false),
                    RevertedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevertedCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportBatches", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ImportBatches_IsReverted_CreatedAt",
                table: "ImportBatches",
                columns: new[] { "IsReverted", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportBatches");
        }
    }
}
