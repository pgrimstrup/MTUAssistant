using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations;

/// <summary>Adds the two distances used to clear fog by line of sight: a scene's Ambient Visual
/// Distance and a character's own Visual Distance, both in metres. Existing scenes and characters
/// start at 0, which leaves the fog to the GM.</summary>
public partial class VisualDistances : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AmbientVisualDistance",
            table: "Scenes",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0
        );

        migrationBuilder.AddColumn<int>(
            name: "VisualDistance",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // SQLite drops these directly; DropColumn would need a table rebuild.
        migrationBuilder.Sql(
            """
            ALTER TABLE Characters DROP COLUMN VisualDistance;
            ALTER TABLE Scenes DROP COLUMN AmbientVisualDistance;
            """
        );
    }
}
