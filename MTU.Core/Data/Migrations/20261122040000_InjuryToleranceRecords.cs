using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MTU.Core.Data.Migrations;

/// <summary>Replaces the single Injury Tolerance value on a character with zero or more
/// Injury Tolerance records. Each character's existing value becomes its first record.</summary>
public partial class InjuryToleranceRecords : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "CharacterInjuryTolerances",
            columns: table => new
            {
                Id = table
                    .Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                CharacterId = table.Column<int>(type: "INTEGER", nullable: false),
                Sequence = table.Column<int>(type: "INTEGER", nullable: false),
                Tolerance = table.Column<int>(type: "INTEGER", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CharacterInjuryTolerances", x => x.Id);
                table.ForeignKey(
                    name: "FK_CharacterInjuryTolerances_Characters_CharacterId",
                    column: x => x.CharacterId,
                    principalTable: "Characters",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade
                );
            }
        );

        migrationBuilder.CreateIndex(
            name: "IX_CharacterInjuryTolerances_CharacterId_Sequence",
            table: "CharacterInjuryTolerances",
            columns: new[] { "CharacterId", "Sequence" }
        );

        // Every non-empty single value becomes that character's first record, unchanged.
        migrationBuilder.Sql(
            """
            INSERT INTO CharacterInjuryTolerances (CharacterId, Sequence, Tolerance)
            SELECT Id, 1, InjuryTolerance FROM Characters WHERE InjuryTolerance <> 0;
            """
        );

        // SQLite drops this directly; DropColumn would need a table rebuild.
        migrationBuilder.Sql("ALTER TABLE Characters DROP COLUMN InjuryTolerance;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "InjuryTolerance",
            table: "Characters",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0
        );

        // The single value can hold only one record, so the records are merged: every option
        // ticked on any record is ticked. Disruptor did not exist before, so it is dropped.
        migrationBuilder.Sql(
            """
            UPDATE Characters SET InjuryTolerance = (
                SELECT MAX(Tolerance & 1) + MAX(Tolerance & 2) + MAX(Tolerance & 4)
                    + MAX(Tolerance & 8) + MAX(Tolerance & 16) + MAX(Tolerance & 32)
                FROM CharacterInjuryTolerances t WHERE t.CharacterId = Characters.Id)
            WHERE Id IN (SELECT CharacterId FROM CharacterInjuryTolerances);
            """
        );

        migrationBuilder.DropTable(name: "CharacterInjuryTolerances");
    }
}
