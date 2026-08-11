using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectSeshat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityDiscoveries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunityDiscoveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    SystemName = table.Column<string>(type: "TEXT", nullable: false),
                    Position = table.Column<string>(type: "TEXT", nullable: true),
                    FirstReportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    LastReportedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ReportCount = table.Column<long>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunityDiscoveries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDiscoveries_LastReportedAt",
                table: "CommunityDiscoveries",
                column: "LastReportedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommunityDiscoveries_SystemName",
                table: "CommunityDiscoveries",
                column: "SystemName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommunityDiscoveries");
        }
    }
}
