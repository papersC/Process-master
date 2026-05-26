using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ESEMS.Web.Migrations
{
    /// <inheritdoc />
    public partial class AddServiceCatalogInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ServiceCatalogInfos",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ServiceId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    DurationEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DurationAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FeesEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FeesAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChannelsEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChannelsAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetAudienceEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TargetAudienceAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreConditionsEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PreConditionsAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PoliciesEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PoliciesAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcedureEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ProcedureAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoryEn = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CategoryAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PublishedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceReference = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    UpdatedById = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCatalogInfos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ServiceCatalogInfos_Services_ServiceId",
                        column: x => x.ServiceId,
                        principalTable: "Services",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCatalogInfos_IsPublished",
                table: "ServiceCatalogInfos",
                column: "IsPublished");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCatalogInfos_ServiceId",
                table: "ServiceCatalogInfos",
                column: "ServiceId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ServiceCatalogInfos");
        }
    }
}
