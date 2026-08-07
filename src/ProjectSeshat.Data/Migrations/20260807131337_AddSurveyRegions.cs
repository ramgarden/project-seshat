using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectSeshat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSurveyRegions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SurveyRegions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CellX = table.Column<int>(type: "INTEGER", nullable: false),
                    CellY = table.Column<int>(type: "INTEGER", nullable: false),
                    CellZ = table.Column<int>(type: "INTEGER", nullable: false),
                    Center = table.Column<string>(type: "TEXT", nullable: false),
                    Score = table.Column<double>(type: "REAL", nullable: false),
                    NearbyVisitedSystems = table.Column<int>(type: "INTEGER", nullable: false),
                    DistanceFromReferenceLy = table.Column<double>(type: "REAL", nullable: false),
                    Surveyed = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SurveyRegions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SurveyRegions_CellX_CellY_CellZ",
                table: "SurveyRegions",
                columns: new[] { "CellX", "CellY", "CellZ" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SurveyRegions");
        }
    }
}
