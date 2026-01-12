using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NyxCEngine.Migrations
{
    /// <inheritdoc />
    public partial class AdjustedBunchOfTablesV1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ThumbnailPath",
                schema: "dbo",
                table: "video_asset",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ThumbnailPath",
                schema: "dbo",
                table: "video_asset");
        }
    }
}
