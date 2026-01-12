using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NyxCEngine.Migrations
{
    /// <inheritdoc />
    public partial class AddClaimsIdTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "claimed_id",
                schema: "dbo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "claimed_id",
                schema: "dbo");
        }
    }
}
