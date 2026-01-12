using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NyxCEngine.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "dbo");

            migrationBuilder.CreateTable(
                name: "customer",
                schema: "dbo",
                columns: table => new
                {
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    CustomerName = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer", x => x.CustomerId);
                });

            migrationBuilder.CreateTable(
                name: "integration_policy",
                schema: "dbo",
                columns: table => new
                {
                    Platform = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PostType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_policy", x => new { x.Platform, x.PostType });
                });

            migrationBuilder.CreateTable(
                name: "background_media",
                schema: "dbo",
                columns: table => new
                {
                    FilePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DurationSeconds = table.Column<double>(type: "float", nullable: true),
                    EndBufferSeconds = table.Column<double>(type: "float", nullable: false, defaultValue: 10.0),
                    Enabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastScannedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_background_media", x => x.FilePath);
                    table.ForeignKey(
                        name: "FK_background_media_customer_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "dbo",
                        principalTable: "customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "customer_background",
                schema: "dbo",
                columns: table => new
                {
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Strategy = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "round_robin"),
                    FixedFilePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    Cursor = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_background", x => x.CustomerId);
                    table.ForeignKey(
                        name: "FK_customer_background_customer_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "dbo",
                        principalTable: "customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "customer_schedule",
                schema: "dbo",
                columns: table => new
                {
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false, defaultValue: "Europe/London"),
                    PostingTimes = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false, defaultValue: "09:00,14:00,20:00"),
                    BufferDays = table.Column<int>(type: "int", nullable: false, defaultValue: 7),
                    JitterMinutes = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_schedule", x => x.CustomerId);
                    table.ForeignKey(
                        name: "FK_customer_schedule_customer_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "dbo",
                        principalTable: "customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "integration",
                schema: "dbo",
                columns: table => new
                {
                    IntegrationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Profile = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Disabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Picture = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration", x => x.IntegrationId);
                    table.ForeignKey(
                        name: "FK_integration_customer_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "dbo",
                        principalTable: "customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "script_item",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FilePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    ContentSha1 = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UsedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReservedUntilUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_script_item", x => x.Id);
                    table.ForeignKey(
                        name: "FK_script_item_customer_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "dbo",
                        principalTable: "customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "video_asset",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    ScriptFilePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    ScriptSha1 = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    WavPath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    TimestampsPath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AudioDurationSeconds = table.Column<double>(type: "float", nullable: true),
                    BackgroundFilePath = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    BackgroundStartOffsetSeconds = table.Column<double>(type: "float", nullable: true),
                    EndBufferSecondsUsed = table.Column<double>(type: "float", nullable: true),
                    Mp4Path = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_video_asset", x => x.Id);
                    table.ForeignKey(
                        name: "FK_video_asset_customer_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "dbo",
                        principalTable: "customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "integration_override",
                schema: "dbo",
                columns: table => new
                {
                    IntegrationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ManualDisabled = table.Column<bool>(type: "bit", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_integration_override", x => x.IntegrationId);
                    table.ForeignKey(
                        name: "FK_integration_override_integration_IntegrationId",
                        column: x => x.IntegrationId,
                        principalSchema: "dbo",
                        principalTable: "integration",
                        principalColumn: "IntegrationId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_post",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Platform = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IntegrationId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PostizPostId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    PostizState = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReleaseUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    AssetId = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "scheduled"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scheduled_post", x => x.Id);
                    table.ForeignKey(
                        name: "FK_scheduled_post_customer_CustomerId",
                        column: x => x.CustomerId,
                        principalSchema: "dbo",
                        principalTable: "customer",
                        principalColumn: "CustomerId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scheduled_post_integration_IntegrationId",
                        column: x => x.IntegrationId,
                        principalSchema: "dbo",
                        principalTable: "integration",
                        principalColumn: "IntegrationId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_scheduled_post_video_asset_AssetId",
                        column: x => x.AssetId,
                        principalSchema: "dbo",
                        principalTable: "video_asset",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "idx_bg_customer",
                schema: "dbo",
                table: "background_media",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_CustomerName",
                schema: "dbo",
                table: "customer",
                column: "CustomerName");

            migrationBuilder.CreateIndex(
                name: "IX_customer_schedule_CustomerId",
                schema: "dbo",
                table: "customer_schedule",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "idx_integration_customer",
                schema: "dbo",
                table: "integration",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "idx_sched_customer_time",
                schema: "dbo",
                table: "scheduled_post",
                columns: new[] { "CustomerId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_post_AssetId",
                schema: "dbo",
                table: "scheduled_post",
                column: "AssetId");

            migrationBuilder.CreateIndex(
                name: "IX_scheduled_post_IntegrationId_ScheduledAtUtc",
                schema: "dbo",
                table: "scheduled_post",
                columns: new[] { "IntegrationId", "ScheduledAtUtc" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "idx_script_customer_unused",
                schema: "dbo",
                table: "script_item",
                columns: new[] { "CustomerId", "UsedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_script_item_FilePath",
                schema: "dbo",
                table: "script_item",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_video_asset_CustomerId",
                schema: "dbo",
                table: "video_asset",
                column: "CustomerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "background_media",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "customer_background",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "customer_schedule",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "integration_override",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "integration_policy",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "scheduled_post",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "script_item",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "integration",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "video_asset",
                schema: "dbo");

            migrationBuilder.DropTable(
                name: "customer",
                schema: "dbo");
        }
    }
}
