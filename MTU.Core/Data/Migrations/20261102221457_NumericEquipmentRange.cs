using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class NumericEquipmentRange : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Armour has no range; older entries used an empty text value.
            migrationBuilder.Sql(
                "UPDATE \"Equipment\" SET \"Range\" = '0' WHERE \"Kind\" = 0 OR trim(\"Range\") = '';"
            );
            migrationBuilder.AlterColumn<double>(
                name: "Range",
                table: "Equipment",
                type: "REAL",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Range",
                table: "Equipment",
                type: "TEXT",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "REAL"
            );
        }
    }
}
