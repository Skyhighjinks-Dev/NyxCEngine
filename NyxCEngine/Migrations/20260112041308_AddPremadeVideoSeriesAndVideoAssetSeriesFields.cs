using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NyxCEngine.Migrations
{
    /// <inheritdoc />
    public partial class AddPremadeVideoSeriesAndVideoAssetSeriesFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_video_asset_CustomerId",
                schema: "dbo",
                table: "video_asset");

            migrationBuilder.AddColumn<int>(
                name: "SeriesCount",
                schema: "dbo",
                table: "video_asset",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SeriesId",
                schema: "dbo",
                table: "video_asset",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SeriesIndex",
                schema: "dbo",
                table: "video_asset",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SourceType",
                schema: "dbo",
                table: "video_asset",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TargetIntegrationId",
                schema: "dbo",
                table: "video_asset",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "premade_video_series",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourcePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    SegmentSeconds = table.Column<int>(type: "int", nullable: false),
                    TargetIntegrationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SplitAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LockOwner = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_premade_video_series", x => x.Id);
                    table.ForeignKey(
                        name: "FK_premade_video_series_customer_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "dbo",
                        principalTable: "customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_video_asset_CustomerId_SourceType",
                schema: "dbo",
                table: "video_asset",
                columns: new[] { "CustomerId", "SourceType" });

            migrationBuilder.CreateIndex(
                name: "IX_video_asset_SeriesId_SeriesIndex",
                schema: "dbo",
                table: "video_asset",
                columns: new[] { "SeriesId", "SeriesIndex" });

            migrationBuilder.CreateIndex(
                name: "IX_video_asset_TargetIntegrationId",
                schema: "dbo",
                table: "video_asset",
                column: "TargetIntegrationId");

            migrationBuilder.CreateIndex(
                name: "IX_premade_video_series_CustomerId",
                schema: "dbo",
                table: "premade_video_series",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_premade_video_series_Status_LockedAtUtc",
                schema: "dbo",
                table: "premade_video_series",
                columns: new[] { "Status", "LockedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "premade_video_series",
                schema: "dbo");

            migrationBuilder.DropIndex(
                name: "IX_video_asset_CustomerId_SourceType",
                schema: "dbo",
                table: "video_asset");

            migrationBuilder.DropIndex(
                name: "IX_video_asset_SeriesId_SeriesIndex",
                schema: "dbo",
                table: "video_asset");

            migrationBuilder.DropIndex(
                name: "IX_video_asset_TargetIntegrationId",
                schema: "dbo",
                table: "video_asset");

            migrationBuilder.DropColumn(
                name: "SeriesCount",
                schema: "dbo",
                table: "video_asset");

            migrationBuilder.DropColumn(
                name: "SeriesId",
                schema: "dbo",
                table: "video_asset");

            migrationBuilder.DropColumn(
                name: "SeriesIndex",
                schema: "dbo",
                table: "video_asset");

            migrationBuilder.DropColumn(
                name: "SourceType",
                schema: "dbo",
                table: "video_asset");

            migrationBuilder.DropColumn(
                name: "TargetIntegrationId",
                schema: "dbo",
                table: "video_asset");

            migrationBuilder.CreateIndex(
                name: "IX_video_asset_CustomerId",
                schema: "dbo",
                table: "video_asset",
                column: "CustomerId");
        }
    }
}
