using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class CompleteChangeRequestLifecycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CancellationReason",
                table: "ChangeRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CancelledAt",
                table: "ChangeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CancelledById",
                table: "ChangeRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImplementedById",
                table: "ChangeRequests",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewStartedAt",
                table: "ChangeRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStartedById",
                table: "ChangeRequests",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CancellationReason",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "CancelledAt",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "CancelledById",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ImplementedById",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ReviewStartedAt",
                table: "ChangeRequests");

            migrationBuilder.DropColumn(
                name: "ReviewStartedById",
                table: "ChangeRequests");
        }
    }
}
