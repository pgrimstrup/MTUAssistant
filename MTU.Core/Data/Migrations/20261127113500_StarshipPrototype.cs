using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations;

/// <summary>Adds a starship's Prototype flag. Purchase and Maintenance Cost are now worked out
/// from the systems; their columns stay and are rewritten when a ship is next saved. Existing
/// ships start as standard designs.</summary>
public partial class StarshipPrototype : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "Prototype",
            table: "Starships",
            type: "INTEGER",
            nullable: false,
            defaultValue: false
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // SQLite drops this directly; DropColumn would need a table rebuild.
        migrationBuilder.Sql("ALTER TABLE Starships DROP COLUMN Prototype;");
    }
}
