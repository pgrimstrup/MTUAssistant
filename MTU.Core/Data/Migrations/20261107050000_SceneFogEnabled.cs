using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class SceneFogEnabled : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FogEnabled",
                table: "Scenes",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // SQLite drops the column natively; EF can't rebuild the table because the previous
            // migration (TrackerInstanceNames) has no target model.
            migrationBuilder.Sql("ALTER TABLE Scenes DROP COLUMN FogEnabled;");
        }
    }
}
