using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ArmourDamageProtection : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProtectionE",
                table: "Equipment",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ProtectionK",
                table: "Equipment",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ProtectionL",
                table: "Equipment",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "ProtectionP",
                table: "Equipment",
                type: "INTEGER",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ProtectionE", table: "Equipment");

            migrationBuilder.DropColumn(name: "ProtectionK", table: "Equipment");

            migrationBuilder.DropColumn(name: "ProtectionL", table: "Equipment");

            migrationBuilder.DropColumn(name: "ProtectionP", table: "Equipment");
        }
    }
}
