using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class PatronsAndEnemies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNpc",
                table: "Travellers",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );

            migrationBuilder.CreateTable(
                name: "PatronsAndEnemies",
                columns: table => new
                {
                    TravellerId = table.Column<int>(type: "INTEGER", nullable: false),
                    Occupation = table.Column<string>(type: "TEXT", nullable: false),
                    Gender = table.Column<string>(type: "TEXT", nullable: false),
                    Quirks1 = table.Column<string>(type: "TEXT", nullable: false),
                    Quirks2 = table.Column<string>(type: "TEXT", nullable: false),
                    SkillLevel = table.Column<int>(type: "INTEGER", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatronsAndEnemies", x => x.TravellerId);
                    table.ForeignKey(
                        name: "FK_PatronsAndEnemies_Travellers_TravellerId",
                        column: x => x.TravellerId,
                        principalTable: "Travellers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PatronsAndEnemies");

            migrationBuilder.DropColumn(name: "IsNpc", table: "Travellers");
        }
    }
}
