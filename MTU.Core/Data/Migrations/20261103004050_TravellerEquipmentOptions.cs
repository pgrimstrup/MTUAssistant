using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class TravellerEquipmentOptions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Fof",
                table: "TravellerEquipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "HolographicSight",
                table: "TravellerEquipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "LaserSight",
                table: "TravellerEquipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "Phud",
                table: "TravellerEquipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<int>(
                name: "Quantity",
                table: "TravellerEquipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1
            );

            migrationBuilder.AddColumn<bool>(
                name: "Scope",
                table: "TravellerEquipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "SmartTracker",
                table: "TravellerEquipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.AddColumn<bool>(
                name: "TargetingSystem",
                table: "TravellerEquipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Fof", table: "TravellerEquipment");

            migrationBuilder.DropColumn(name: "HolographicSight", table: "TravellerEquipment");

            migrationBuilder.DropColumn(name: "LaserSight", table: "TravellerEquipment");

            migrationBuilder.DropColumn(name: "Phud", table: "TravellerEquipment");

            migrationBuilder.DropColumn(name: "Quantity", table: "TravellerEquipment");

            migrationBuilder.DropColumn(name: "Scope", table: "TravellerEquipment");

            migrationBuilder.DropColumn(name: "SmartTracker", table: "TravellerEquipment");

            migrationBuilder.DropColumn(name: "TargetingSystem", table: "TravellerEquipment");
        }
    }
}
