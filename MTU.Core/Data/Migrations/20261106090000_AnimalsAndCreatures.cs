using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations;

public partial class AnimalsAndCreatures : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "IsAnimal",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: false
        );
        migrationBuilder.CreateTable(
            name: "Animals",
            columns: table => new
            {
                CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                Hits = table.Column<int>(type: "INTEGER", nullable: false),
                AnimalType = table.Column<string>(type: "TEXT", nullable: false),
                Behaviour = table.Column<string>(type: "TEXT", nullable: false),
                Traits = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Animals", x => x.CharacterId);
                table.ForeignKey(
                    "FK_Animals_Characters_CharacterId",
                    x => x.CharacterId,
                    "Characters",
                    "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );
        migrationBuilder.CreateTable(
            name: "AnimalAttacks",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                Name = table.Column<string>(type: "TEXT", nullable: false),
                Damage = table.Column<string>(type: "TEXT", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AnimalAttacks", x => x.Id);
                table.ForeignKey(
                    "FK_AnimalAttacks_Animals_CharacterId",
                    x => x.CharacterId,
                    "Animals",
                    "CharacterId",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );
        migrationBuilder.CreateIndex(
            name: "IX_AnimalAttacks_CharacterId",
            table: "AnimalAttacks",
            column: "CharacterId"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "AnimalAttacks");
        migrationBuilder.DropTable(name: "Animals");
        migrationBuilder.DropColumn(name: "IsAnimal", table: "Characters");
    }
}
