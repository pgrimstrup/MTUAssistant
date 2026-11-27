using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations;

/// <summary>Adds a starship's Type (its class or role, such as Free Trader). Existing ships start
/// with none.</summary>
public partial class StarshipClassification : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Classification",
            table: "Starships",
            type: "TEXT",
            nullable: false,
            defaultValue: ""
        );
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // SQLite drops this directly; DropColumn would need a table rebuild.
        migrationBuilder.Sql("ALTER TABLE Starships DROP COLUMN Classification;");
    }
}
