using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentTechLevelVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Equipment_Kind_Name", table: "Equipment");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Kind_Name_Tl",
                table: "Equipment",
                columns: new[] { "Kind", "Name", "Tl" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Equipment_Kind_Name_Tl", table: "Equipment");

            migrationBuilder.CreateIndex(
                name: "IX_Equipment_Kind_Name",
                table: "Equipment",
                columns: new[] { "Kind", "Name" }
            );
        }
    }
}
