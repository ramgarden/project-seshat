using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectSeshat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNavigationState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NavigationStates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CurrentSystemId = table.Column<long>(type: "INTEGER", nullable: true),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NavigationStates", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_NavigationStates_Id",
                table: "NavigationStates",
                column: "Id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NavigationStates");
        }
    }
}
