using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProjectSeshat.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBeaconScans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Beacons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BeaconName = table.Column<string>(type: "TEXT", nullable: false),
                    BeaconType = table.Column<string>(type: "TEXT", nullable: true),
                    BeaconOwner = table.Column<string>(type: "TEXT", nullable: true),
                    SystemName = table.Column<string>(type: "TEXT", nullable: false),
                    SystemAddress = table.Column<long>(type: "INTEGER", nullable: true),
                    ObservedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    Fingerprint = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beacons", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Beacons_Fingerprint",
                table: "Beacons",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beacons_SystemName",
                table: "Beacons",
                column: "SystemName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Beacons");
        }
    }
}
