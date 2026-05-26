using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationDedupKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                "IF COL_LENGTH('Notifications','DedupKey') IS NULL " +
                "ALTER TABLE [Notifications] ADD [DedupKey] nvarchar(450) NULL;");

            migrationBuilder.Sql(
                "IF NOT EXISTS (SELECT 1 FROM sys.indexes " +
                "WHERE name = 'IX_Notifications_UserId_DedupKey' AND object_id = OBJECT_ID('Notifications')) " +
                "CREATE INDEX [IX_Notifications_UserId_DedupKey] ON [Notifications] ([UserId], [DedupKey]);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_UserId_DedupKey",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "DedupKey",
                table: "Notifications");
        }
    }
}
