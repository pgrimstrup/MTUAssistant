using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations;

/// <summary>Adds the polygons drawn on a scene's Battlemap, stored as JSON beside the fog state.
/// Existing scenes start with none.</summary>
public partial class ScenePolygons : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PolygonState",
            table: "Scenes",
            type: "TEXT",
            nullable: true
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // SQLite drops the column directly; DropColumn would need a table rebuild.
        migrationBuilder.Sql("ALTER TABLE Scenes DROP COLUMN PolygonState;");
    }
}
