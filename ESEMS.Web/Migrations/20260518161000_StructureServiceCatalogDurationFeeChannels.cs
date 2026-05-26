using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class StructureServiceCatalogDurationFeeChannels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChannelsAr",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "ChannelsEn",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "DurationAr",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "DurationEn",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "FeesAr",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "FeesEn",
                table: "ServiceCatalogInfos");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryChannels",
                table: "ServiceCatalogInfos",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DurationUnit",
                table: "ServiceCatalogInfos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DurationValue",
                table: "ServiceCatalogInfos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "FeeAmount",
                table: "ServiceCatalogInfos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeeNote",
                table: "ServiceCatalogInfos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsFree",
                table: "ServiceCatalogInfos",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DeliveryChannels",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "DurationUnit",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "DurationValue",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "FeeAmount",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "FeeNote",
                table: "ServiceCatalogInfos");

            migrationBuilder.DropColumn(
                name: "IsFree",
                table: "ServiceCatalogInfos");

            migrationBuilder.AddColumn<string>(
                name: "ChannelsAr",
                table: "ServiceCatalogInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChannelsEn",
                table: "ServiceCatalogInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DurationAr",
                table: "ServiceCatalogInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DurationEn",
                table: "ServiceCatalogInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeesAr",
                table: "ServiceCatalogInfos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FeesEn",
                table: "ServiceCatalogInfos",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
