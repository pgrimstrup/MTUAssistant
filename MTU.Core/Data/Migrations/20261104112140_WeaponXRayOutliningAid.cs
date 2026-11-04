using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class WeaponXRayOutliningAid : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "XRayOutliningAid",
                table: "CharacterEquipment",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "XRayOutliningAid", table: "CharacterEquipment");
        }
    }
}
